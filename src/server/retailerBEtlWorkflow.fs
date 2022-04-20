// ETL logic for retailer B's csv file
module RetailerB.Etl

open System
open System.IO
open System.Data

open Newtonsoft.Json

open FSharp.Data

open Etl.Dtos

module Dtos = 
    [<Literal>]
    let schema =  "sku (string), url (string), description (string option), name (string), product_group (string), price (int), currency (string), last_update (date)" 
    type ProductCsv =
        CsvProvider<
            Separators = ",",
            HasHeaders = false,
            Schema = schema,
            IgnoreErrors = true
        > 

let sourceName = "retailerB"

// parse the contents of the file into an array
let extractProductsFromCsv (file:Stream) = 
    Dtos.ProductCsv.Load(file).Rows
    |> Seq.toArray

// try to find a product from the json file by sku. If it's not already in the database, then create a new one 
let upsertProduct now (p:Dtos.ProductCsv.Row) conn = 
    async {
        let! queryRes =
            conn
            |> SqlPersistenceModel.findProductByExternalId ("sku", p.Sku, sourceName)

        match queryRes with
        | Some product -> 
            printfn "found product: %s" product.id
            return { productId = product.id; action = Created; data = p }
        | None ->
            let! productId = conn |> SqlPersistenceModel.createProduct

            printfn "create product: %s" productId

            let extId : SqlPersistenceModel.Dtos.ProductExternalId = {
                productId = productId
                externalIdType = "sku" 
                externalIdValue = p.Sku 
                source = sourceName 
                observedOn = p.Last_update 
                ingestedOn = DateTime.toUnixTs now 
            }

            do! conn
                |> SqlPersistenceModel.insertProductExternalIds [extId]
                |> Async.Ignore

            return { productId = productId; action = Updated; data = p }
    }

// record all of the product attributes from the json file.
let insertProductAttributes now productId (p:Dtos.ProductCsv.Row) conn = 
    async {
        let attr : SqlPersistenceModel.Dtos.ProductAttributeValue = {
            id = productId
            name = "";
            value = None;
            source = sourceName;
            observedOn = p.Last_update
            ingestedOn = DateTime.toUnixTs now 
        }

        return!
            conn
            |> SqlPersistenceModel.insertProductAttributes
                [
                    { attr with name = "url"; value = Some p.Url } 
                    { attr with name = "description"; value = p.Description } 
                    { attr with name = "price"; value = Some (string p.Price) }
                    { attr with name = "currency"; value = Some p.Currency }
                    { attr with name = "category"; value = Some (Etl.normalizeProductCategory p.Product_group) } 
                ]
    }

let etlWorkflow (connectToDb:unit -> IDbConnection) (file:Stream) = 
    async {
        let now = DateTime.UtcNow

        return! 
            file
            // this should probably use streams to handle large files/input
            |> extractProductsFromCsv
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
