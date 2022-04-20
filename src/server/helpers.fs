namespace System

[<AutoOpen>]
module Operators = 
    let inline (=>) a b = a, box b

[<AutoOpen>]
module DateTimeExts = 
    module DateTime =
        open System
        let private epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
        let toJs(d:DateTime) = (d.Ticks - epoch.Ticks) / 10000L
        let toUnixTs(d:DateTime) = (d - epoch).TotalSeconds |> int64
        let fromUnixTs(ts:uint64) = epoch.AddSeconds(float ts)