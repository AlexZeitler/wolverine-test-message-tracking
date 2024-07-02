using Alba;
using JasperFx.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Wolverine;
using Wolverine.Tracking;

namespace WolverineMessageTracking.IntegrationTests;

public class RandomFileChange
{
  private readonly IMessageBus _messageBus;

  public RandomFileChange(
    IMessageBus messageBus
  ) => _messageBus = messageBus;

  public async Task SimulateRandomFileChange()
  {
    // Delay task with a random number of milliseconds
    // Here would be your FileSystemWatcher / IFileProvider
    await Task.Delay(
      TimeSpan.FromMilliseconds(
        new Random().Next(100, 1000)
      )
    );
    var randomFileName = Path.GetRandomFileName();
    await _messageBus.SendAsync(new FileAdded(randomFileName));
  }
}

public class FileAddedHandler
{
  public Task Handle(
    FileAdded message
  ) =>
    Task.CompletedTask;
}

public record FileAdded(string FileName);

[TestFixture]
public class When_message_is_sent
{
  private IAlbaHost _host;

  [SetUp]
  public async Task InitializeAsync()
  {
    var hostBuilder = Host.CreateDefaultBuilder();
    hostBuilder.ConfigureServices(
      services => { services.AddSingleton<RandomFileChange>(); }
    );
    hostBuilder.UseWolverine();

    _host = await hostBuilder.StartAlbaAsync();
  }

  [Test]
  public async Task should_be_in_session()
  {
    var randomEventEmitter = _host.Services.GetRequiredService<RandomFileChange>();

    var session = await _host
      .TrackActivity()
      .Timeout(2.Seconds())
      .ExecuteAndWaitAsync(
        (Func<IMessageContext, Task>)(
          async (
            _
          ) => await randomEventEmitter.SimulateRandomFileChange()
        )
      );


    session.Sent.AllMessages()
      .Count()
      .ShouldBe(1);

    session.Sent.AllMessages()
      .First()
      .ShouldBeOfType<FileAdded>();
  }

  [TearDown]
  public async Task DisposeAsync() => await _host.DisposeAsync();
}
