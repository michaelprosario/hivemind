using Microsoft.AspNetCore.Mvc.Testing;
namespace FileRepo.Web.IntegrationTests;
public class ApiTests
{
    [Test] public async Task Swagger_IsAvailable() { using var factory = new WebApplicationFactory<Program>(); using var client = factory.CreateClient(); var response = await client.GetAsync("/swagger/v1/swagger.json"); Assert.That(response.IsSuccessStatusCode, Is.True); }
}
