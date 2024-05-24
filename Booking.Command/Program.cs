using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Booking.Command.Models;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(x =>
    x.AddDefaultPolicy(p =>
    p.AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()
    ));

var app = builder.Build();

app.MapPost("/book", async ([FromBody] BookingRequest request) =>
{
    var token = request.IdToken;
    var idTokenDetails = new JwtSecurityToken(token);

    var userId = idTokenDetails.Claims.FirstOrDefault(x => x.Type == "sub")?.Value ?? "";
    var givenName = idTokenDetails.Claims.FirstOrDefault(x => x.Type == "givenName")?.Value ?? "";
    var familyName = idTokenDetails.Claims.FirstOrDefault(x => x.Type == "familyName")?.Value ?? "";
    var email = idTokenDetails.Claims.FirstOrDefault(x => x.Type == "email")?.Value ?? "";
    var phoneNumber = idTokenDetails.Claims.FirstOrDefault(x => x.Type == "phoneNumber")?.Value ?? "";

    var dto = new BookingDto
    {
        Id = Guid.NewGuid().ToString(),
        HotelId = request.HotelId,
        CheckinDate = request.CheckinDate,
        CheckoutDate = request.CheckoutDate,
        UserId = userId,
        GivenName = givenName,
        FamilyName = familyName,
        Email = email,
        PhoneNumber = phoneNumber,
        Status = BookingStatus.Pending
    };

    using var dbClient = new AmazonDynamoDBClient();
    using var dbContext = new DynamoDBContext(dbClient);
    await dbContext.SaveAsync(dto);
});

app.MapGet("/health", () => new HttpResponseMessage(HttpStatusCode.OK));


app.Run();

