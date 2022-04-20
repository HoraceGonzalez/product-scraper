open System

module CompositionRoot =
    open System
    open System.Threading

    open Suave
    open Suave.Filters
    open Suave.RequestErrors
    open Suave.Successful
    open Suave.Operators
    open Suave.Web

    let dbConnectionFactory() =
        Sql.openConnection(
            "sql",
            "products",
            "root",
            "password",
            3306
        )
    let handleFileUpload =
        RouteHandlers.handleFileUpload
            (RetailerA.Etl.etlWorkflow dbConnectionFactory)
            (RetailerB.Etl.etlWorkflow dbConnectionFactory)

    let apiRoutes =
        let usage = 
            """
            <h2>Try using <strong>curl</strong> to upload a file</h2>

            <ul>
                <li><code>curl -F 'file=@sampleData/retailer A.json;type=application/x.retailerA+json' localhost:8080/upload</code></li>
                <li><code>curl -F 'file=@sampleData/retailer B.csv;type=application/x.retailerB+csv' localhost:8080/upload</code></li>
            <ul>
            """
        choose [
            GET >=> path "/product" >=> RouteHandlers.json NOT_FOUND (Error "Would've implemented some endpoints to query the product data, here.")  
            POST >=> path "/upload" >=> handleFileUpload 
            OK usage >=> Writers.setHeader "content-type" "text/html"
        ]

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
