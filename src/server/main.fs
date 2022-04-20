open System

// The composition root is where all the dependencies are assempled. 
module CompositionRoot =
    open System
    open System.Threading

    open Suave
    open Suave.Filters
    open Suave.RequestErrors
    open Suave.Successful
    open Suave.Operators
    open Suave.Web

    // opens a new mysql connection
    let dbConnectionFactory() =
        Sql.openConnection(
            "sql",
            "products",
            "root",
            "password",
            3306
        )

    // This should probably push each upload onto a queue for async processing, but we're doing it all synchronously for now
    let handleFileUpload =
        RouteHandlers.handleFileUpload
            (RetailerA.Etl.etlWorkflow dbConnectionFactory)
            (RetailerB.Etl.etlWorkflow dbConnectionFactory)

    // a usage message explaining how to make HTTP requests to the service
    let usage = 
        """
        <h2>Try using <strong>curl</strong> to upload a file</h2>

        <ul>
            <li><code>curl -F 'file=@sampleData/retailer A.json;type=application/x.retailerA+json' localhost:8080/upload</code></li>
            <li><code>curl -F 'file=@sampleData/retailer B.csv;type=text/x.retailerB+csv' localhost:8080/upload</code></li>
        <ul>
        """

    // The http route tree
    let apiRoutes =
        choose [
            GET >=> path "/product" >=> RouteHandlers.json NOT_FOUND (Error "Would've implemented some endpoints to query the product data, here.")  
            POST >=> path "/upload" >=> handleFileUpload 
            // otherwise just print the usage
            OK usage >=> Writers.setHeader "content-type" "text/html"
        ]

// This is the entrypoint of the program. It runs any sql migrations and starts the api web server
[<EntryPoint>]
let main args = 
    async {
        Sql.registerHandlers()

        use conn = CompositionRoot.dbConnectionFactory()
        do! conn |> SqlPersistenceModel.Migrations.runAll

        // should gracefully terminate with posix SIGTERM, SIGINT
        // not enough time, so ctrl+c cmd+c should do the trick 
        let (serverTask,cancel) =
            ApiServer.startWebserver(CompositionRoot.apiRoutes)

        let! _ = serverTask

        return 0
    }
    |> Async.RunSynchronously
