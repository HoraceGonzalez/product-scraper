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

// a DTO to represent the response shape
type ResponseEnvelope<'body> = {
    success : bool
    body : 'body option 
    error : string option
}

// a json serialization function
let serialize o = JsonConvert.SerializeObject(o, Formatting.Indented, Json.settings)

// convert a Result type to a json response envelope
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

// a web helper that serializes the response envelope and sets the content type to application/json
let json responseStatusCode res = 
    let json =
        res
        |> toResponseEnvelope
        |> serialize 
    responseStatusCode json
    >=> Writers.setHeader "Content-Type" "application/json"

// a wrapper around file processing that catches and handles any exceptions.
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

        // look for a file in the HTTP request and key off the mime type.
        // the two recognized mimetypes are:
        // - application/x.retailerA+json 
        // - text/x.retailerA+csv
        // I named these so it'd be clear that they have a particular format -- not just plain csv/json
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

