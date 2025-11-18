using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("keycloak", 8090)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithBindMount(
        Path.GetFullPath("infra/keycloak/Workly-realm.json"),
        "/opt/keycloak/data/import/Workly-realm.json")
    .WithBindMount(
        Path.GetFullPath("infra/keycloak/master-realm.json"),
        "/opt/keycloak/data/import/master-realm.json");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("workly");

var apiService = builder.AddProject<Projects.tp_aspire_samy_jugurtha_ApiService>("apiservice")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(keycloak)
    .WaitFor(keycloak);

var webapp = builder.AddProject<Projects.tp_aspire_samy_jugurtha_WebApp>("webapp")
    .WithReference(apiService)
    .WithReference(keycloak)
    .WaitFor(apiService)
    .WaitFor(keycloak);

builder.Build().Run();