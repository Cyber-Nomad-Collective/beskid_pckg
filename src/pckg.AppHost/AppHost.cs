var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume();

var pckgDb = postgres.AddDatabase("pckgdb");

builder
    .AddProject<Projects.Server>("server")
    .WithReference(pckgDb)
    .WithEnvironment("Database__Provider", "postgresql")
    .WaitFor(pckgDb);

builder.Build().Run();
