namespace System

[<AutoOpen>]
module Operators = 
    let inline (=>) a b = a, box b

// some datetime functions for generating unix timestamps
[<AutoOpen>]
module DateTimeExts = 
    module DateTime =
        open System
        let toUnixTs(d:DateTime) = (d - DateTime.UnixEpoch).TotalSeconds |> int64
        let fromUnixTs(ts:uint64) = DateTime.UnixEpoch.AddSeconds(float ts)

// Some json serialization helpers 
module Json =
    open Newtonsoft.Json
    open Microsoft.FSharpLu.Json
    let settings = JsonSerializerSettings(
        Converters = [| CompactUnionJsonConverter() |],
        DateParseHandling= DateParseHandling.DateTime
    )
    let serializer = JsonSerializer.Create(settings)