module Domain

type Product = {
    name : string
    description : string option
    gtin : Gtin option
    sku : Sku option
    msrp : Price option
}
and Category = 
    | VideoGames of VideoGameData
and VideoGameData = {
    platform : string
}
and Price = {
    amount : int
    currency : string // USD, CAD, AUD, GBP, etc...
}
and Sku = {
    value : string
    retailer : Retailer
}
and Retailer = {
    name : string 
    website : string option
    id : string
}
and Gtin = 
    | Upc of string
    | Isbn10 of string 
    | Ean of string