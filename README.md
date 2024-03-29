# gnet-dump

An application to export GCP networks & subnets to a json file.

# Pre-requisites

You must be able to generate an access token sourced from an authorised GCP account. The easier way to do that is to [install the gcloud tools](https://cloud.google.com/sdk/docs/quickstarts) and log in.

# How to Run

The following will output the network configuration for the _example-project_ to the example-project-networks.json file.

```
docker run -it -v $(pwd):/output csbronner/gnet-dump \
    --access-token $(gcloud auth print-access-token) \
    --project-id example-project \
    --skip-default false \
    --output-file /output/example-project-networks.json
```

If you have a proxy between your host and the gcp api endpoints, pass the __HTTPS_PROXY__ environment variable:

```
docker run -it -v $(pwd):/output csbronner/gnet-dump \
    -e HTTPS_PROXY="http://localhost:5000" \
    --access-token $(gcloud auth print-access-token) \
    --project-id example-project \
    --skip-default false \
    --output-file /output/example-project-networks.json
```

For all projects accessible by the user identified by the passed access token, leave off the _--project-id_ option.

```
docker run -it -v $(pwd):/output csbronner/gnet-dump \
    --access-token $(gcloud auth print-access-token) \
    --skip-default false \
    --output-file /output/example-project-networks.json
```
