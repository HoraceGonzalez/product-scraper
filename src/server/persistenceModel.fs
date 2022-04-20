module SqlPersistenceModel

// Some DTOs to represent sql database records
module Dtos =
    type UnixTimestamp = int64

    [<CLIMutable>]
    type Product = {
        id : string 
        createdOn : DateTime 
        modifiedOn : DateTime 
    }

    [<CLIMutable>]
    type ProductExternalId = {
        productId : string
        externalIdType : string 
        externalIdValue : string 
        source : string 
        observedOn : DateTime 
        ingestedOn : UnixTimestamp 
    }

    [<CLIMutable>]
    type ProductAttributeValue = {
        id : string
        name : string
        value : string option
        source : string
        observedOn : DateTime
        ingestedOn : UnixTimestamp
    }

// Sql table and field names constants.
// would consider using an ORM for this kind of stuff in production.
// or could've at least used nameof operator on DTO field names
[<AutoOpen>]
module Schema = 
    module Products =
        let tableName = "products"
        module Field =
            let id = "id"
            let createdOn = "createdOn"
            let modifiedOn = "modifiedOn"

    module ProductsExternalIds =
        let tableName = "products_external_ids"
        module Field =
            let productId = "productId"
            let externalIdType = "externalIdType"
            let externalIdValue = "externalIdValue"
            let source = "source"
            let observedOn = "observedOn"
            let ingestedOn = "ingestedOn"

    module ProductsMetadata =
        let tableName = "products_metadata"
        module Field = 
            let productId = "productId"
            let attributeName = "attributeName"
            let attributeValue = "attributeValue"
            let source = "source"
            let observedOn = "observedOn"
            let ingestedOn = "ingestedOn"

    module Migrations =
        let tableName = "migrations"
        module Field =
            let version = "version"

// insert a new product record
let createProduct (conn) =
    async {
        let now = DateTime.UtcNow
        let productId = Guid.NewGuid().ToString()
        let! _ =
            conn
            |> Sql.execute 
                $"""
                insert into {Schema.Products.tableName} (
                    {Schema.Products.Field.id},
                    {Schema.Products.Field.createdOn},
                    {Schema.Products.Field.modifiedOn}
                )
                values (
                    @productId,
                    @createdOn,
                    @modifiedOn
                )
                """
                [
                    "@productId" => productId
                    "@createdOn" => now
                    "@modifiedOn" => now
                ]

        return productId
    }

// adds product "external IDs" to storage. This could be a sku, or some kind of gtin 
let insertProductExternalIds (attrs: Dtos.ProductExternalId seq) (conn) =
    let insertClause (index:int) (attr:Dtos.ProductExternalId) =
        $"insert into {Schema.ProductsExternalIds.tableName} (
            {Schema.ProductsExternalIds.Field.productId},
            {Schema.ProductsExternalIds.Field.externalIdType},
            {Schema.ProductsExternalIds.Field.externalIdValue},
            {Schema.ProductsExternalIds.Field.source},
            {Schema.ProductsExternalIds.Field.observedOn},
            {Schema.ProductsExternalIds.Field.ingestedOn}
        )
        values (
            @productId_{index},
            @externalIdType_{index},
            @externalIdValue_{index},
            @source_{index},
            @observedOn_{index},
            @ingestedOn_{index}
        );", ([
            "productId" => attr.productId
            "externalIdType" => attr.externalIdType
            "externalIdValue" => attr.externalIdValue
            "source" => attr.source
            "observedOn" => attr.observedOn
            "ingestedOn" => attr.ingestedOn
        ]
        |> List.map (fun (key,value) -> $"{key}_{index}", value))

    let inserts = attrs |> Seq.mapi (insertClause)

    let sql =
        [
            yield "start transaction;"
            yield! inserts |> Seq.map fst
            yield "commit;"
        ]
        |> String.concat "\n"
    
    let sqlParams = inserts |> Seq.collect snd

    conn
    |> Sql.execute sql sqlParams


// stores product meta data using entity-attribute-value convention. There are pros and cons, but
// it allows for schema flexibility at the expense of queryability.
// each attribute also records the source it came from, when it was ingested,
// and when it was last "observed" in the wild
let insertProductAttributes (attrs: Dtos.ProductAttributeValue seq) (conn) =
    let insertClause (index:int) (attr:Dtos.ProductAttributeValue) =
        $"insert into {Schema.ProductsMetadata.tableName} (
            {Schema.ProductsMetadata.Field.productId},
            {Schema.ProductsMetadata.Field.attributeName},
            {Schema.ProductsMetadata.Field.attributeValue},
            {Schema.ProductsMetadata.Field.source},
            {Schema.ProductsMetadata.Field.observedOn},
            {Schema.ProductsMetadata.Field.ingestedOn} 
        )
        values (
            @productId_{index},
            @attributeName_{index},
            @attributeValue_{index},
            @source_{index},
            @observedOn_{index},
            @ingestedOn_{index}
        );", ([
            "productId" => attr.id
            "attributeName" => attr.name
            "attributeValue" => attr.value
            "source" => attr.source
            "observedOn" => attr.observedOn
            "ingestedOn" => attr.ingestedOn
        ]
        |> List.map (fun (key,value) -> $"{key}_{index}", value))

    let inserts = attrs |> Seq.mapi (insertClause)

    let sql =
        [
            yield "start transaction;"
            yield! inserts |> Seq.map fst
            yield "commit;"
        ]
        |> String.concat "\n"
    
    let sqlParams = inserts |> Seq.collect snd

    conn
    |> Sql.execute sql sqlParams

// gets a product by the canonical product ID that we created for this application
let getProduct (productId:string) conn = 
    async {
        let! res =
            conn 
            |> Sql.query<Dtos.Product> 
                $"""
                select * from {Schema.Products.tableName}
                where {Schema.Products.Field.id} = @productId
                limit 1
                """
                ["@productId" => productId]
        return Seq.tryHead res
    }

// gets a product by sku, gtin, etc... 
let findProductByExternalId (idType:string, idValue:string, retailer:string) (conn) = 
    async {
        let! ids =
            conn
            |> Sql.query<Dtos.ProductExternalId>
                $"""
                select * from {Schema.ProductsExternalIds.tableName}
                where {Schema.ProductsExternalIds.Field.externalIdType} = @externalIdType
                and {Schema.ProductsExternalIds.Field.externalIdValue} = @externalIdValue
                and {Schema.ProductsExternalIds.Field.source} = @retailer
                limit 1
                """
                [
                    "@externalIdType" => idType
                    "@externalIdValue" => idValue
                    "@retailer" => retailer
                ]
        
        match Seq.tryHead ids with
        | Some externalId -> 
            return! conn |> getProduct externalId.productId
        | None ->
            return None
    }

// jerry-rigged sql migrations library
module Migrations = 
    let initializeMigrationsTable conn = 
        conn
        |> Sql.execute
            $"""
            create table if not exists {Schema.Migrations.tableName} (
                {Schema.Migrations.Field.version} int
            );
            """
            []
    
    let queryCurrentSchemaVersion conn = 
        async {
            let! rows = 
                conn
                |> Sql.query<{| version : int |}>
                    $"""
                    select {Schema.Migrations.Field.version}
                    from {Schema.Migrations.tableName}
                    limit 1
                    """
                    []
            return Seq.tryHead rows
        }

    let putCurrentSchemaVersion (version:int) conn = 
        async {
            let! currentVersion =
                conn
                |> queryCurrentSchemaVersion

            let! rows = 
                match currentVersion with
                | Some _ -> 
                    conn
                    |> Sql.execute
                        $"""
                        update {Schema.Migrations.tableName}
                        set {Schema.Migrations.Field.version} = @version
                        """
                        ["@version" => version]
                | None -> 
                    conn
                    |> Sql.execute
                        $"""
                        insert into {Schema.Migrations.tableName} (
                            {Schema.Migrations.Field.version}
                        )
                        values (@version) 
                        """
                        ["@version" => version]
            return rows 
        }

    let createProductsTable conn = 
        conn
        |> Sql.execute
            $"""
            start transaction;
            create table {Schema.Products.tableName} (
                {Schema.Products.Field.id} varchar (255) primary key,
                {Schema.Products.Field.createdOn} datetime,
                {Schema.Products.Field.modifiedOn} datetime 
            );
            create index idx_{Schema.Products.Field.modifiedOn} on {Schema.Products.tableName} ({Schema.Products.Field.modifiedOn} desc);
            commit;
            """
            []

    let createProductsMetadataTable conn = 
        conn
        |> Sql.execute
            $"""
            start transaction;
            create table {Schema.ProductsMetadata.tableName} (
                {Schema.ProductsMetadata.Field.productId} varchar (255),
                {Schema.ProductsMetadata.Field.attributeName} varchar (255),
                {Schema.ProductsMetadata.Field.attributeValue} varchar (255),
                {Schema.ProductsMetadata.Field.source} varchar (255),
                {Schema.ProductsMetadata.Field.observedOn} datetime,
                {Schema.ProductsMetadata.Field.ingestedOn} bigint unsigned,
                foreign key ({Schema.ProductsMetadata.Field.productId}) references {Schema.Products.tableName}({Schema.Products.Field.id}) on delete cascade
            );
            create index idx_{Schema.ProductsMetadata.Field.productId} on {Schema.ProductsMetadata.tableName} ({Schema.ProductsMetadata.Field.productId});
            create index idx_{Schema.ProductsMetadata.Field.attributeName} on {Schema.ProductsMetadata.tableName} ({Schema.ProductsMetadata.Field.attributeName});
            create index idx_{Schema.ProductsMetadata.Field.attributeValue} on {Schema.ProductsMetadata.tableName} ({Schema.ProductsMetadata.Field.attributeValue});
            create index idx_{Schema.ProductsMetadata.Field.ingestedOn} on {Schema.ProductsMetadata.tableName} ({Schema.ProductsMetadata.Field.ingestedOn} desc);
            commit;
            """
            []
    
    let createProductExternalIdsTable conn = 
        conn
        |> Sql.execute
            $"""
            start transaction;
            create table {Schema.ProductsExternalIds.tableName} (
                {Schema.ProductsExternalIds.Field.productId} varchar (255),
                {Schema.ProductsExternalIds.Field.externalIdType} varchar (255),
                {Schema.ProductsExternalIds.Field.externalIdValue} varchar (255),
                {Schema.ProductsExternalIds.Field.source} varchar (255),
                {Schema.ProductsExternalIds.Field.observedOn} datetime,
                {Schema.ProductsExternalIds.Field.ingestedOn} bigint unsigned,
                foreign key ({Schema.ProductsExternalIds.Field.productId}) references {Schema.Products.tableName}({Schema.Products.Field.id}) on delete cascade
            );
            create index idx_{Schema.ProductsExternalIds.Field.productId} on {Schema.ProductsExternalIds.tableName} ({Schema.ProductsExternalIds.Field.productId});
            create index idx_{Schema.ProductsExternalIds.Field.externalIdType} on {Schema.ProductsExternalIds.tableName} ({Schema.ProductsExternalIds.Field.externalIdType});
            create index idx_{Schema.ProductsExternalIds.Field.externalIdValue} on {Schema.ProductsExternalIds.tableName} ({Schema.ProductsExternalIds.Field.externalIdValue});
            create index idx_{Schema.ProductsExternalIds.Field.ingestedOn} on {Schema.ProductsExternalIds.tableName} ({Schema.ProductsExternalIds.Field.ingestedOn} desc);
            commit;
            """
            []

    let allMigrations = 
        [
            createProductsTable
            createProductExternalIdsTable
            createProductsMetadataTable
        ]

    let runAll (conn) = 
        async {
            let! _ = conn |> initializeMigrationsTable
            let! version = conn |> queryCurrentSchemaVersion
            let version =
                version
                |> Option.map (fun r -> r.version)
                |> Option.defaultValue -1 
            do!
                allMigrations
                |> Seq.mapi (fun i migration -> i, migration)
                |> Seq.skip (version + 1)
                |> Seq.map (fun (i,migration) ->
                    async {
                        let! _ = conn |> migration
                        let! _ = conn |> putCurrentSchemaVersion i
                        return i
                    })
                |> Async.Sequential
                |> Async.Ignore
        }

