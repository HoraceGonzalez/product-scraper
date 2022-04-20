// A SqL client library that provides basic ORM functionality by wrapping Dapper
module Sql 

open System
open System.Data

open Dapper
open Dapper.FSharp

open MySqlConnector

let registerHandlers() =
    OptionTypes.register()

do registerHandlers()

// opens a new mysql database connection. 
let openConnection (server:string, database:string, user:string, pass:string, port:int) =
    let connStr =
        [
            "Server", server
            "Database", database
            "Uid", user
            "Pwd", pass
            "Port", string port
        ]
        |> Seq.map (fun (k,v) -> $"{k}={v}")
        |> String.concat ";"

    let csb = MySqlConnectionStringBuilder(connStr)
    let conn = new MySqlConnection(csb.ConnectionString)
    conn.Open()
    conn :> Data.IDbConnection

// do a sql query given a sql query string and some params. 
// note: could've added support for transactions, but didn't have enough time
let query<'record>
    (sql:string)
    (queryParams:(string*obj) seq)
    (conn:IDbConnection)
    : Async<'record seq> =
    conn.QueryAsync<'record>(sql, dict queryParams)
    |> Async.AwaitTask

// execute a sql commend given a sql command string and some params
// note: could've added support for transactions, but didn't have enough time
let execute
    (sql:string)
    (queryParams:(string*obj) seq)
    (conn:IDbConnection)
    : Async<int> =
    conn.ExecuteAsync(sql, dict queryParams)
    |> Async.AwaitTask

let dropTable (tableName:string) = execute $"drop table {tableName};" []
