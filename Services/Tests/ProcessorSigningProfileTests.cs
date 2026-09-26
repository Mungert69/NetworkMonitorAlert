using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Objects.Repository.Helpers;
using NetworkMonitor.Objects.ServiceMessage;
using Xunit;

namespace NetworkMonitorAlert.Tests.Services;

public sealed class ProcessorSigningProfileTests
{
    [Theory]
    [InlineData("processorAlertFlag", 1)]
    [InlineData("processorAlertSent", 1)]
    [InlineData("processorResetAlerts", 1)]
    [InlineData("processorAlertFlag", 2)]
    [InlineData("processorAlertSent", 2)]
    [InlineData("processorResetAlerts", 2)]
    public async Task AlertMessagesRemainUnchangedForConfiguredEcdsaProcessor(string operation, int topology)
    {
        var transport = new Mock<IRabbitRepo>();
        var mlDsa = new Mock<IBackendMessageSignatureService>(MockBehavior.Strict);
        var ecdsa = new Mock<IProcessorCommandSigner>(MockBehavior.Strict);
        string target = ProcessorRabbitTopology.GetRoutingId("esp-device");
        ecdsa.Setup(s => s.RequiresEcdsa(target)).Returns(true);
        var repo = new BackendSignedRabbitRepo(transport.Object, mlDsa.Object, ecdsa.Object);
        var ids = new List<int> { 3, 7 };

        await ProcessorRabbitPublisher.PublishAsync(repo, "esp-device", operation, ids, topology);

        if (topology == 1)
            transport.Verify(r => r.PublishAsync(operation + "esp-device", ids, ""), Times.Once);
        else
            transport.Verify(r => r.PublishAsync(ProcessorRabbitTopology.CommandsExchange, ids,
                ProcessorRabbitTopology.BuildRoutingKey(target, operation)), Times.Once);
        transport.VerifyNoOtherCalls();
        mlDsa.VerifyNoOtherCalls();
        ecdsa.Verify(s => s.Sign(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task ProtectedOperationUsesSharedEcdsaProfile()
    {
        var transport = new Mock<IRabbitRepo>();
        var mlDsa = new Mock<IBackendMessageSignatureService>(MockBehavior.Strict);
        var ecdsa = new Mock<IProcessorCommandSigner>();
        string target = ProcessorRabbitTopology.GetRoutingId("esp-device");
        var command = new ProcessorInitObj();
        var envelope = new ProcessorSignedCommand { Payload = "test", Signature = "test" };
        ecdsa.Setup(s => s.RequiresEcdsa(target)).Returns(true);
        ecdsa.Setup(s => s.Sign("processorInit", target, command)).Returns(envelope);
        var repo = new BackendSignedRabbitRepo(transport.Object, mlDsa.Object, ecdsa.Object);

        await ProcessorRabbitPublisher.PublishAsync(repo, "esp-device", "processorInit", command, 2);

        transport.Verify(r => r.PublishAsync(ProcessorRabbitTopology.CommandsExchange, envelope,
            ProcessorRabbitTopology.BuildRoutingKey(target, "processorInit")), Times.Once);
        mlDsa.VerifyNoOtherCalls();
    }
}
