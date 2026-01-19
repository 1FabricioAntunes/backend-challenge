using MediatR;
using TransactionProcessor.Application.DTOs;

namespace TransactionProcessor.Application.UseCases.Stores.Queries;

/// <summary>
/// Query to retrieve all stores with balances.
/// </summary>
public class GetStoresQuery : IRequest<List<StoreDto>>
{
}
