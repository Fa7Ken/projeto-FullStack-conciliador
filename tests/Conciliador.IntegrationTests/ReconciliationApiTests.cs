using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Conciliador.Application.Auth;
using Conciliador.Application.Reconciliations;
using Conciliador.Domain.Reconciliations;
using Conciliador.TestData;
using FluentAssertions;

namespace Conciliador.IntegrationTests;

public class ReconciliationApiTests : IClassFixture<ApiFactory>
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly SampleScenario Scenario = SampleScenario.Create(salesPerDay: 8);

    private readonly ApiFactory _factory;

    public ReconciliationApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, "wrong"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoints_require_a_token()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/reconciliations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/reconciliations", new MultipartFormDataContent { { new StringContent("Debit"), "operation" } }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Runs_a_reconciliation_and_keeps_it_in_the_history()
    {
        var client = await AuthenticatedClientAsync();
        using var form = Form("Debit", "2026-03-01", "2026-03-31");
        AddFile(form, "salesFiles", "relatorio-vendas-2026-02.xlsx", Scenario.FebruarySalesWorkbook());
        AddFile(form, "salesFiles", "relatorio-vendas-2026-03.xlsx", Scenario.MarchSalesWorkbook());
        AddFile(form, "statementFiles", "extrato-2026-03.xlsx", Scenario.MarchStatementWorkbook());

        var response = await client.PostAsync("/api/reconciliations", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ReconciliationDetailDto>(Json);
        created!.Summary.Operation.Should().Be(OperationType.Debit);
        created.Summary.CreatedBy.Should().Be(ApiFactory.AdminEmail);
        created.Summary.DivergentLines.Should().Be(1);
        created.Summary.TotalDifference.Should().Be(SampleScenario.PosRentalDiscount);
        created.Lines.Should().ContainSingle(l => l.Status == LineStatus.SalesAboveStatement && l.Brand == "Mastercard");
        created.Brands.Select(b => b.Brand).Should().BeEquivalentTo("Elo", "Mastercard", "Visa");
        created.Sources.Should().HaveCount(3);
        response.Headers.Location!.ToString().Should().EndWith($"/api/reconciliations/{created.Summary.Id}");

        var fetched = await client.GetFromJsonAsync<ReconciliationDetailDto>($"/api/reconciliations/{created.Summary.Id}", Json);
        fetched!.Lines.Should().BeEquivalentTo(created.Lines);
        fetched.Summary.TotalSales.Should().Be(created.Summary.TotalSales);

        var list = await client.GetFromJsonAsync<PagedResult<ReconciliationSummaryDto>>("/api/reconciliations?page=1&pageSize=50", Json);
        list!.Items.Should().Contain(i => i.Id == created.Summary.Id);
        list.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Explains_when_the_files_do_not_cover_the_selected_range()
    {
        var client = await AuthenticatedClientAsync();
        using var form = Form("Credit", "2026-07-01", "2026-07-31");
        AddFile(form, "salesFiles", "relatorio-vendas-2026-03.xlsx", Scenario.MarchSalesWorkbook());
        AddFile(form, "statementFiles", "extrato-2026-03.xlsx", Scenario.MarchStatementWorkbook());

        var response = await client.PostAsync("/api/reconciliations", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ReconciliationDetailDto>(Json);
        result!.Lines.Should().BeEmpty();
        result.Issues.Select(i => i.Code).Should().Contain(new[] { IssueCodes.NoDataInRange, IssueCodes.RangeOutsideStatementPeriod });
    }

    [Fact]
    public async Task Returns_422_when_a_file_is_on_the_wrong_side()
    {
        var client = await AuthenticatedClientAsync();
        using var form = Form("Debit", "2026-03-01", "2026-03-31");
        AddFile(form, "salesFiles", "extrato-2026-03.xlsx", Scenario.MarchStatementWorkbook());
        AddFile(form, "statementFiles", "extrato-2026-03.xlsx", Scenario.MarchStatementWorkbook());

        var response = await client.PostAsync("/api/reconciliations", form);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain("não parece ser um relatório de vendas");
        problem.GetProperty("fileName").GetString().Should().Be("extrato-2026-03.xlsx");
    }

    [Fact]
    public async Task Returns_400_with_messages_when_fields_are_missing()
    {
        var client = await AuthenticatedClientAsync();
        using var form = new MultipartFormDataContent { { new StringContent("Debit"), "operation" } };

        var response = await client.PostAsync("/api/reconciliations", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Informe a data inicial de pagamento.").And.Contain("Envie pelo menos um arquivo de extrato.");
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_reconciliation()
    {
        var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/reconciliations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private static MultipartFormDataContent Form(string operation, string startDate, string endDate) => new()
    {
        { new StringContent(operation), "operation" },
        { new StringContent(startDate), "startDate" },
        { new StringContent(endDate), "endDate" }
    };

    private static void AddFile(MultipartFormDataContent form, string field, string fileName, byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(XlsxContentType);
        form.Add(file, field, fileName);
    }
}
