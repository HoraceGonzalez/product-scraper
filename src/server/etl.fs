module Etl

module Dtos = 
   // a data type to represent the result of a product upload 
   type UploadResult<'data> = {
      productId : string 
      action : UploadEffect
      data : 'data 
   }
   and UploadEffect = 
      | Created
      | Updated

// There should be a standard product category taxonomy that the system maintains. Here's just using strings, here. 
let normalizeProductCategory (raw:string) =
   // do some magic here
   raw.ToLowerInvariant()
