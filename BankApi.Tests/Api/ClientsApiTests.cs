using System.Net;
using System.Net.Http.Json;
using BankApi.Contracts;
using BankApi.Domain;

namespace BankApi.Tests.Api;

public class ClientsApiTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    [Fact]
    public async Task Request_without_token_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/api/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { username = BankApiFactory.AdminUsername, password = "wrong-password" });

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Client_can_be_created_read_updated_and_deleted()
    {
        var api = await factory.CreateAdminClientAsync();
        var email = $"{Guid.NewGuid():N}@Example.com";

        var created = await (await api.PostAsJsonAsync("/api/clients", new
        {
            lastName = " Ivanov ",
            firstName = "Ivan",
            middleName = "Ivanovich",
            email,
            birthDate = "1990-05-17"
        })).ReadAsync<ClientResponse>();

        Assert.Equal("Ivanov", created.LastName);
        Assert.Equal(email.ToLowerInvariant(), created.Email);
        Assert.Equal(new DateOnly(1990, 5, 17), created.BirthDate);

        var updated = await (await api.PutAsJsonAsync($"/api/clients/{created.Id}", new
        {
            lastName = "Petrov",
            firstName = "Ivan",
            email,
            birthDate = "1990-05-18"
        })).ReadAsync<ClientResponse>();

        Assert.Equal("Petrov", updated.LastName);
        Assert.Null(updated.MiddleName);

        var loaded = await (await api.GetAsync($"/api/clients/{created.Id}")).ReadAsync<ClientResponse>();
        Assert.Equal(new DateOnly(1990, 5, 18), loaded.BirthDate);

        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/api/clients/{created.Id}")).StatusCode);
        await (await api.GetAsync($"/api/clients/{created.Id}")).ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_client_is_rejected_with_400()
    {
        var api = await factory.CreateAdminClientAsync();

        var response = await api.PostAsJsonAsync("/api/clients", new
        {
            lastName = "",
            firstName = "Ivan",
            email = "not-an-email",
            birthDate = "1990-05-17"
        });

        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("LastName", problem!.Errors.Keys);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Birth_date_in_the_future_is_rejected_with_400()
    {
        var api = await factory.CreateAdminClientAsync();

        var response = await api.PostAsJsonAsync("/api/clients", new
        {
            lastName = "Ivanov",
            firstName = "Ivan",
            email = $"{Guid.NewGuid():N}@example.com",
            birthDate = "2999-01-01"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_with_409()
    {
        var api = await factory.CreateAdminClientAsync();
        var existing = await api.CreateClientAsync();

        var response = await api.PostAsJsonAsync("/api/clients", new
        {
            lastName = "Other",
            firstName = "Person",
            email = existing.Email.ToUpperInvariant(),
            birthDate = "1980-01-01"
        });

        await response.ReadProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_is_filtered_and_paginated()
    {
        var api = await factory.CreateAdminClientAsync();
        var family = $"Fam{Guid.NewGuid():N}"[..12];
        var a = await api.CreateClientAsync($"{family}A", birthDate: "1980-01-01");
        var b = await api.CreateClientAsync($"{family}B", birthDate: "1990-01-01");
        var c = await api.CreateClientAsync($"{family}C", birthDate: "2000-01-01");

        var firstPage = await (await api.GetAsync($"/api/clients?search={family}&pageSize=2"))
            .ReadAsync<PagedResult<ClientResponse>>();
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(new[] { a.Id, b.Id }, firstPage.Items.Select(x => x.Id));

        var secondPage = await (await api.GetAsync($"/api/clients?search={family}&pageSize=2&page=2"))
            .ReadAsync<PagedResult<ClientResponse>>();
        Assert.Equal(new[] { c.Id }, secondPage.Items.Select(x => x.Id));

        var byBirthDate = await (await api.GetAsync(
                $"/api/clients?search={family}&birthDateFrom=1985-01-01&birthDateTo=2000-01-01&sortBy=BirthDate&desc=true"))
            .ReadAsync<PagedResult<ClientResponse>>();
        Assert.Equal(new[] { c.Id, b.Id }, byBirthDate.Items.Select(x => x.Id));

        var byEmail = await (await api.GetAsync($"/api/clients?email={b.Email}"))
            .ReadAsync<PagedResult<ClientResponse>>();
        Assert.Equal(b.Id, Assert.Single(byEmail.Items).Id);
    }

    [Fact]
    public async Task List_is_filtered_by_phone()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var number = $"+99655{Random.Shared.Next(1000000, 9999999)}";
        await (await api.PostAsJsonAsync($"/api/clients/{client.Id}/phones", new { number, type = "Mobile" }))
            .ReadAsync<PhoneResponse>();

        var found = await (await api.GetAsync($"/api/clients?phone={number[1..]}"))
            .ReadAsync<PagedResult<ClientResponse>>();

        var item = Assert.Single(found.Items);
        Assert.Equal(client.Id, item.Id);
        Assert.Equal(number, Assert.Single(item.Phones).Number);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Invalid_paging_is_rejected_with_400(string query)
    {
        var api = await factory.CreateAdminClientAsync();

        var response = await api.GetAsync($"/api/clients?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Phones_can_be_managed()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        var other = await api.CreateClientAsync();
        var url = $"/api/clients/{client.Id}/phones";

        var phone = await (await api.PostAsJsonAsync(url, new { number = "+996555123456", type = "Mobile" }))
            .ReadAsync<PhoneResponse>();
        Assert.Equal(PhoneType.Mobile, phone.Type);

        await (await api.PostAsJsonAsync(url, new { number = "+996555123456", type = "Work" }))
            .ReadProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await api.PostAsJsonAsync(url, new { number = "12-34", type = "Mobile" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await api.PostAsJsonAsync(url, new { number = "+996555000000", type = "Fax" })).StatusCode);

        var updated = await (await api.PutAsJsonAsync($"{url}/{phone.Id}", new { number = "+996312654321", type = "Home" }))
            .ReadAsync<PhoneResponse>();
        Assert.Equal(PhoneType.Home, updated.Type);

        // A phone is addressable only through its own client.
        await (await api.GetAsync($"/api/clients/{other.Id}/phones/{phone.Id}")).ReadProblemAsync(HttpStatusCode.NotFound);

        Assert.Single(await (await api.GetAsync(url)).ReadAsync<List<PhoneResponse>>());
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"{url}/{phone.Id}")).StatusCode);
        Assert.Empty(await (await api.GetAsync(url)).ReadAsync<List<PhoneResponse>>());
    }

    [Fact]
    public async Task Operator_cannot_delete_client()
    {
        var admin = await factory.CreateAdminClientAsync();
        var @operator = await factory.CreateOperatorClientAsync();
        var client = await admin.CreateClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await @operator.DeleteAsync($"/api/clients/{client.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await @operator.GetAsync($"/api/clients/{client.Id}")).StatusCode);
    }

    [Fact]
    public async Task Client_with_accounts_cannot_be_deleted()
    {
        var api = await factory.CreateAdminClientAsync();
        var client = await api.CreateClientAsync();
        await api.CreateAccountAsync(client.Id);

        await (await api.DeleteAsync($"/api/clients/{client.Id}")).ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
    }
}
