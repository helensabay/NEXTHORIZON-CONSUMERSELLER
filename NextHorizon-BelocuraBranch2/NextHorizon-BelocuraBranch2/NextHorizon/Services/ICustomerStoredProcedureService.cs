using NextHorizon.Models;

namespace NextHorizon.Services;

public interface ICustomerStoredProcedureService
{
    Task<IReadOnlyList<Customer>> GetLatestCustomersAsync(int top = 10, CancellationToken cancellationToken = default);
}
