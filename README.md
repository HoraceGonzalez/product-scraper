## Setup

- install docker/docker-compose
- clone this repository
- `docker-compose up`
- The service will start on localhost:8080. (It could take a few mins to cold-start)

## Usage 

Here are some sample `curl` commands to upload data to the service

```curl -F 'file=@sampleData/retailer A.json;type=application/x.retailerA+json' localhost:8080/upload```
```curl -F 'file=@sampleData/retailer B.csv;type=application/x.retailerB+csv' localhost:8081/upload```

