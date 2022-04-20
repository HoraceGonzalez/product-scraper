module RouteHandlers

open System
open System.IO
open System.Threading

open Suave
open Suave.Filters
open Suave.RequestErrors
open Suave.Successful
open Suave.Operators
open Suave.Web

open Newtonsoft.Json

type ResponseEnvelope<'body> = {
    success : bool
    body : 'body option 
    error : string option
}

let serialize o = JsonConvert.SerializeObject(o, Formatting.Indented, Json.settings)

let toResponseEnvelope (res:Result<_,_>) = 
    match res with
    | Ok body -> 
        {
            success = true
            body = Some body
            error = None
        }
    | Error err -> 
        {
            success = false
            body = None 
            error = Some (err.ToString())
        }

let json responseStatusCode res = 
    let json =
        res
        |> toResponseEnvelope
        |> serialize 
    responseStatusCode json
    >=> Writers.setHeader "Content-Type" "application/json"

let processFile etlWorkflow (file:Stream) =
    async {
        try
            let! res = etlWorkflow file
            return Ok res
        with
        | ex -> 
            printfn "%A" ex
            return Error "unhandled exception."
    }

let handleFileUpload retailerAWorkflow retailerBWorkflow = request (fun req ctx ->
    async {
        let file = 
            req.files
            |> Seq.tryHead

        match file with
        | Some file when file.mimeType = "application/x.retailerA+json" ->
            use file = File.OpenRead file.tempFilePath
            let! res = processFile retailerAWorkflow file
            return! json OK res ctx
        | Some file when file.mimeType = "text/x.retailerB+csv" ->
            use file = File.OpenRead file.tempFilePath
            let! res = processFile retailerBWorkflow file
            return! json OK res ctx
        | _ ->
            let err = Error "please upload a file: retailerA.json or retailerB.csv"
            return! json BAD_REQUEST err ctx
    })

