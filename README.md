## Setup

- install docker/docker-compose
- clone this repository
- `docker-compose up`
- The service will start on localhost:8080. You'll see the message below on the command prompt. (It could take a few mins to cold-start)

```bash
Smooth! Suave listener started in 28.371ms with binding 0.0.0.0:8080
```

## Usage 

Here are some sample `curl` commands to upload data to the service

```curl -F 'file=@sampleData/retailer A.json;type=application/x.retailerA+json' localhost:8080/upload```
```curl -F 'file=@sampleData/retailer B.csv;type=text/x.retailerB+csv' localhost:8080/upload```

