module ApiServer

open System
open System.Threading
open System.Net

open Suave
open Suave.Web

// just a helper to start the api web server
let startWebserver (routes) =
    let cts = new CancellationTokenSource()
    let cfg =
        { defaultConfig with
            // normally would use a reverse proxy like nginx to expose the service,
            // but using 0.0.0.0 for demonstration purposes
            bindings = [ HttpBinding.create HTTP IPAddress.Any 8080us ]
            cancellationToken = cts.Token
        }
    let listening, serverTask = startWebServerAsync cfg routes 
    serverTask, cts