// ETL logic for retailer A's json file
module RetailerA.Etl

open System
open System.IO
open System.Data

open Newtonsoft.Json

open Etl.Dtos

// a data type to represent a json item in the file
module Dtos = 
    type Product = {
        sku : string
        url : string
        description : string option
        price : int 
        currency : string
        name : string
        category : string 
        platform : string
        last_updated : DateTime
    }

let sourceName = "retailerA"

// parse the contents of the file into an array
let extractProductsFromJson(file:Stream) = 
    let serializer = JsonSerializer()
    use streamReader = new StreamReader(file)
    use jsonReader = new JsonTextReader(streamReader)
    Json.serializer.Deserialize<Dtos.Product array>(jsonReader)

// try to find a product from the json file by sku. If it's not already in the database, then create a new one 
let upsertProduct now (p:Dtos.Product) conn = 
    async {
        let! queryRes =
            conn
            |> SqlPersistenceModel.findProductByExternalId ("sku", p.sku, sourceName)

        match queryRes with
        | Some product -> 
            printfn "found product: %s" product.id
            return { productId = product.id; action = Updated; data = p }
        | None ->
            let! productId = conn |> SqlPersistenceModel.createProduct

            printfn "create product: %s" productId

            let extId : SqlPersistenceModel.Dtos.ProductExternalId = {
                productId = productId
                externalIdType = "sku" 
                externalIdValue = p.sku 
                source = sourceName 
                observedOn = p.last_updated 
                ingestedOn = DateTime.toUnixTs now 
            }

            do! conn
                |> SqlPersistenceModel.insertProductExternalIds [extId]
                |> Async.Ignore

            return { productId = productId; action = Updated; data = p }
    }

// record all of the product attributes from the json file.
let insertProductAttributes now productId (p:Dtos.Product) conn = 
    async {
        let attr : SqlPersistenceModel.Dtos.ProductAttributeValue = {
            id = productId
            name = "";
            value = None;
            source = sourceName;
            observedOn = p.last_updated
            ingestedOn = DateTime.toUnixTs now 
        }

        return!
            conn
            |> SqlPersistenceModel.insertProductAttributes
                [
                    { attr with name = "url"; value = Some p.url } 
                    { attr with name = "description"; value = p.description } 
                    { attr with name = "price"; value = Some (string p.price) }
                    { attr with name = "currency"; value = Some p.currency }
                    { attr with name = "category"; value = Some (Etl.normalizeProductCategory p.category) } 
                    { attr with name = "videogame_platform"; value = Some p.platform } 
                ]
    }

let etlWorkflow (connectToDb:unit -> IDbConnection) (file:Stream) = 
    async {
        let now = DateTime.UtcNow

        return! 
            file
            // this should probably use streams to handle large files/input
            |> extractProductsFromJson
            |> Seq.map (fun p ->
                async {
                    // Everything that follows should happen a sql transaction, but I'm leaving this out for simplicity 
                    use conn = connectToDb()
                    let! uploadResult = conn |> upsertProduct now p
                    let! _ = conn |> insertProductAttributes now uploadResult.productId p
                    return uploadResult
                })
            |> Async.Sequential
    }



