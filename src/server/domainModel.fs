module Domain

// ideally this is how I'd model a "product" in purse domain logic. 
// It's pretty slean, but I tried to incorporate at least the data in the sample files 
// Note: this domain model is just for reference; it isn't used anywhere in the project. I ran out of time, but the idea was to 
// rehydrate from the entity-attribute-value representation of the product data stored in the database.
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