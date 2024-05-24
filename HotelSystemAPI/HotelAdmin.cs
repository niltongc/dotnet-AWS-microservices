using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AutoMapper;
using HotelSystemAPI.models;
using HttpMultipartParser;


[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]



namespace HotelSystemAPI
{
    public class HotelAdmin
    {
        public async Task<APIGatewayProxyResponse> ListHotels(APIGatewayProxyRequest request)
        {
            var response = new APIGatewayProxyResponse
            {
                Headers = new Dictionary<string, string>(),
                StatusCode = 200
            };

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,POST");
            response.Headers.Add("Content-Type", "application/json");

            var token = request.QueryStringParameters["token"];
            var tokenDetails = new JwtSecurityToken(token);
            var userId = tokenDetails.Claims.FirstOrDefault(x => x.Type == "sub");

            var region = Environment.GetEnvironmentVariable("AWS_REGION");

            var client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
            var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(region));

            using var dbContext = new DynamoDBContext(dbClient);

            var hotels = await dbContext.ScanAsync<Hotel>(new []{new ScanCondition("UserId",ScanOperator.Equal, userId)}).GetRemainingAsync();

            response.Body = JsonSerializer.Serialize(hotels);


            return response;
        }

        public async Task<APIGatewayProxyResponse> AddHotel(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse
            {
                Headers = new Dictionary<string, string>(),
                StatusCode = 200
            };

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,POST");

            var bodyContent = request.IsBase64Encoded
                ? Convert.FromBase64String(request.Body)
                : Encoding.UTF8.GetBytes(request.Body);

            using var memStream = new MemoryStream(bodyContent);
            var formData = await MultipartFormDataParser.ParseAsync(memStream).ConfigureAwait(false); ;

            var hotelName = formData.GetParameterValue("hotelName");
            var hotelRating = formData.GetParameterValue("hotelRating");
            var hotelCity = formData.GetParameterValue("hotelCity");
            var hotelPrice = formData.GetParameterValue("hotelPrice");

            var file = formData.Files.FirstOrDefault();
            var fileName = file.FileName;
            

            await using var fileContentStream = new MemoryStream();
            await file.Data.CopyToAsync(fileContentStream);
            fileContentStream.Position = 0;

            var userId = formData.GetParameterValue("userId");
            var idToken = formData.GetParameterValue("idToken");

            var token = new JwtSecurityToken(jwtEncodedString: idToken);
            var group = token.Claims.FirstOrDefault(x => x.Type == "cognito:groups");
            if (group == null || group.Value != "Admin")
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Body = JsonSerializer.Serialize(new { Error = "Unauthorised. Must be a member of Admin group." });
            }

            var region = Environment.GetEnvironmentVariable("AWS_REGION");
            var bucketName = Environment.GetEnvironmentVariable("bucketName");

            var client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
            var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(region));
            

            try
            {
                await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName,
                    InputStream = fileContentStream,
                    AutoCloseStream = true,
                });

                var hotel = new Hotel
                {
                    UserId = userId,
                    Id = Guid.NewGuid().ToString(),
                    Name = hotelName,
                    CityName = hotelCity,
                    Price = int.Parse(hotelPrice),
                    Rating = int.Parse(hotelPrice),
                    FileName = fileName
                };

                using var dbContext = new DynamoDBContext(dbClient);
                await dbContext.SaveAsync(hotel);

                var mapperConfig = new MapperConfiguration(cfg => 
                cfg.CreateMap<Hotel, HotelCreatedEvent>()
                    .ForMember(dest => dest.CreationDateTime,
                    opt => opt.MapFrom(src => DateTime.Now))
                );

                var mapper = new Mapper(mapperConfig);
                var hotelCreatedEvent = mapper.Map<Hotel, HotelCreatedEvent>(hotel);

                var snsClient = new AmazonSimpleNotificationServiceClient();
                var publishResponse = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = Environment.GetEnvironmentVariable("snsTopicArn"),
                    Message = JsonSerializer.Serialize(hotelCreatedEvent)
                });


            }
            catch (Exception)
            {

                throw;
            }

            Console.WriteLine("OK.");

            return response;
        }
    }
}
