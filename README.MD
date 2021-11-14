# Metadata Exctractor
This is a simple Lambda function that can respond to S3 events, read images uploaded to an S3 bucket, extract the image metadata and save it alongside the images for easy retrieval.

# Requirements
* .Net 5.0.

# Setup
You can run the project using the [Mock Lambda Test Tool](https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool) to accurately simulate incoming image notifications.

# Deploying
The function can be deployed by using the included profile. You may need to modify the profile's role, which needs to have the following rights:
* Basic Lambda execution rights.
* Cloudwatch create and post rights.
* S3 read/write rights.
Once the function is deployed, an S3 Put notification needs to be setup on the S3 bucket(s) with incoming images to be processed.

# Helpful
[Metadata extractor](https://github.com/drewnoakes/metadata-extractor-dotnet) did the heavy lifting of extracting metadata.