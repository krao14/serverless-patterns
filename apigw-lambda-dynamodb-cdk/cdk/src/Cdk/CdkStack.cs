using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;
using Constructs;

namespace Cdk
{
    public class CdkStack : Stack
    {
        internal CdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var tableName = "MyCdkTable";
            
            var buildCommands = new[]
            {
                "cd /asset-input",
                "export DOTNET_CLI_HOME=\"/tmp/DOTNET_CLI_HOME\"",
                "export PATH=\"$PATH:/tmp/DOTNET_CLI_HOME/.dotnet/tools\"",
                "dotnet tool install -g Amazon.Lambda.Tools",
                "dotnet lambda package -o output.zip",
                "unzip -o -d /asset-output output.zip"
            };
            
            var dynamoDbTable = new Table(this, "MyCdkTable", new TableProps()
            {
                BillingMode = BillingMode.PAY_PER_REQUEST,
                TableName = tableName,
                PartitionKey = new Attribute()
                {
                    Name = "PK",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute()
                {
                    Name = "SK",
                    Type = AttributeType.STRING
                }
            });

            var lambdaHandlerRole = new Role(this, "DynamoDbHandlerRole", new RoleProps()
            {
                RoleName = "DynamoDbHandlerRole",
                Description = "Role assumed by the DynamoDbLambdaFunction",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            });

            var apiGatewayIntegrationRole = new Role(this, "ApiGatewayIntegrationRole", new RoleProps() {
                AssumedBy = new ServicePrincipal("apigateway.amazonaws.com"),
            });
            
            var handler = new Function(this, "DynamoDbHandler", new FunctionProps()
            {
                Runtime = Runtime.DOTNET_CORE_3_1,
                Timeout = Duration.Seconds(30),
                Environment = new Dictionary<string, string>(1)
                {
                    {"TABLE_NAME", tableName}
                },
                Code = Code.FromAsset("code/src/DynamoDbLambda", new AssetOptions()
                {
                    Bundling = new BundlingOptions
                    {
                        Image  = Runtime.DOTNET_CORE_3_1.BundlingImage,
                        Command = new []
                        {
                            "bash", "-c", string.Join(" && ", buildCommands)
                        }
                    }
                }),
                Handler = "DynamoDbLambda::DynamoDbLambda.Function::FunctionHandler",
                Role = lambdaHandlerRole
            });

            var apiGateway = new RestApi(this, "CdkApi", new RestApiProps()
            {
                RestApiName = "CdkApi"
            });

            apiGateway.Root.AddMethod("ANY");
            
            var postResource = apiGateway.Root.AddResource("create");
            postResource.AddMethod("POST", new LambdaIntegration(handler));

            handler.GrantInvoke(apiGatewayIntegrationRole);
            dynamoDbTable.GrantReadWriteData(lambdaHandlerRole);
        }
    }
}
