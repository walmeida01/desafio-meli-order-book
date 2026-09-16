using Xunit;

namespace OrderBook.FunctionalTests.Fixtures;

[CollectionDefinition("API real collection", DisableParallelization = true)]
public sealed class ApiCollection : ICollectionFixture<ApiCollectionFixture>;
