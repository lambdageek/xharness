﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.Orchestration;

public class TestOrchestratorTests : OrchestratorTestBase
{
    private readonly TestOrchestrator _testOrchestrator;
    private readonly Mock<IAppTester> _appTester;
    private readonly Mock<IAppTesterFactory> _appTesterFactory;

    private const string SuccessResultLine = "Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0";

    public TestOrchestratorTests()
    {
        _appTester = new();
        _appTesterFactory = new();
        _appTesterFactory.SetReturnsDefault(_appTester.Object);

        // Prepare succeeding install/uninstall as we don't care about those in the test/run tests
        _appInstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));

        _appUninstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));

        _testOrchestrator = new(
            _appBundleInformationParser.Object,
            _appInstaller.Object,
            _appUninstaller.Object,
            _appTesterFactory.Object,
            _deviceFinder.Object,
            _logger.Object,
            _logs,
            _mainLog.Object,
            _errorKnowledgeBase.Object,
            _diagnosticsData,
            _helpers.Object);
    }

    [Fact]
    public async Task OrchestrateSimulatorTestTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        var envVars = new[] { ("envVar1", "value1"), ("envVar2", "value2") };

        _appTester
            .Setup(x => x.TestApp(
                _appBundleInformation,
                testTarget,
                _simulator.Object,
                null,
                TimeSpan.FromMinutes(30),
                It.IsAny<TimeSpan>(),
                false,
                It.IsAny<IEnumerable<string>>(),
                envVars,
                It.IsAny<XmlResultJargon>(),
                It.IsAny<string[]?>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.Succeeded, SuccessResultLine))
            .Verifiable();

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel,
            XmlResultJargon.xUnit,
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeWirelessDevices: false,
            resetSimulator: true,
            enableLldb: false,
            signalAppEnd: false,
            envVars,
            Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false, true, It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySimulatorReset(true);
        VerifySimulatorCleanUp(true);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _simulator.Object, It.IsAny<CancellationToken>()),
            Times.Once);

        _appTester.VerifyAll();
    }

    [Fact]
    public async Task OrchestrateDeviceTestTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");

        var extraArguments = new[] { "--some arg1", "--some arg2" };

        _appTester
            .Setup(x => x.TestApp(
                _appBundleInformation,
                testTarget,
                _device.Object,
                null,
                TimeSpan.FromMinutes(30),
                It.IsAny<TimeSpan>(),
                false,
                extraArguments,
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<XmlResultJargon>(),
                It.IsAny<string[]?>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.Succeeded, SuccessResultLine))
            .Verifiable();

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath,
            testTarget,
            DeviceName,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel,
            XmlResultJargon.xUnit,
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeWirelessDevices: true,
            resetSimulator: false,
            enableLldb: false,
            signalAppEnd: false,
            Array.Empty<(string, string)>(),
            extraArguments,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, DeviceName, It.IsAny<ILog>(), true, true, It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _device.Object, It.IsAny<CancellationToken>()),
            Times.Once);

        _appTester.VerifyAll();
    }

    [Fact]
    public async Task OrchestrateFailedSimulatorTestTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        _appTester
            .Setup(x => x.TestApp(
                _appBundleInformation,
                testTarget,
                _simulator.Object,
                null,
                TimeSpan.FromMinutes(30),
                It.IsAny<TimeSpan>(),
                false,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<XmlResultJargon>(),
                It.IsAny<string[]?>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.Crashed, "App never reported back"))
            .Verifiable();

        var failure = new KnownIssue("Some failure", suggestedExitCode: (int)ExitCode.APP_CRASH);
        _errorKnowledgeBase
            .Setup(x => x.IsKnownTestIssue(It.IsAny<IFileBackedLog>(), out failure))
            .Returns(true)
            .Verifiable();

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel,
            XmlResultJargon.xUnit,
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: true,
            signalAppEnd: false,
            Array.Empty<(string, string)>(),
            Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.APP_CRASH, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false, true, It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _simulator.Object, It.IsAny<CancellationToken>()),
            Times.Once);

        _errorKnowledgeBase.VerifyAll();
        _appTester.VerifyAll();
    }

    [Fact]
    public async Task OrchestrateTimedOutSimulatorTestTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        _appTester
            .Setup(x => x.TestApp(
                _appBundleInformation,
                testTarget,
                _simulator.Object,
                null,
                TimeSpan.FromMinutes(30),
                It.IsAny<TimeSpan>(),
                false,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<XmlResultJargon>(),
                It.IsAny<string[]?>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.Crashed, "App never reported back"))
            .Verifiable();

        var failure = new KnownIssue("Some failure", suggestedExitCode: (int)ExitCode.APP_CRASH);
        _errorKnowledgeBase
            .Setup(x => x.IsKnownTestIssue(It.IsAny<IFileBackedLog>(), out failure))
            .Returns(true)
            .Verifiable();

        var cts = new CancellationTokenSource();

        _deviceFinder.Reset();
        _deviceFinder
            .Setup(x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false, true, It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new DevicePair(_simulator.Object, null));

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel,
            XmlResultJargon.xUnit,
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: true,
            signalAppEnd: false,
            Array.Empty<(string, string)>(),
            Array.Empty<string>(),
            cts.Token);

        // Verify
        Assert.Equal(ExitCode.APP_LAUNCH_TIMEOUT, result);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
    }

    [Fact]
    public async Task OrchestrateTimedOutSimulatorSearchTestTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        _deviceFinder.Reset();
        _deviceFinder
            .Setup(x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel,
            XmlResultJargon.xUnit,
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: true,
            signalAppEnd: false,
            Array.Empty<(string, string)>(),
            Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.APP_LAUNCH_TIMEOUT, result);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
    }

    [Fact]
    public async Task OrchestrateFailedDeviceTestTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");

        var extraArguments = new[] { "--some arg1", "--some arg2" };

        _appTester
            .Setup(x => x.TestApp(
                _appBundleInformation,
                testTarget,
                _device.Object,
                null,
                TimeSpan.FromMinutes(30),
                It.IsAny<TimeSpan>(),
                true,
                extraArguments,
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<XmlResultJargon>(),
                It.IsAny<string[]?>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.TimedOut, SuccessResultLine))
            .Verifiable();

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath,
            testTarget,
            DeviceName,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel,
            XmlResultJargon.xUnit,
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeWirelessDevices: true,
            resetSimulator: false,
            enableLldb: false,
            signalAppEnd: true,
            Array.Empty<(string, string)>(),
            extraArguments,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.TIMED_OUT, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, DeviceName, It.IsAny<ILog>(), true, true, It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _device.Object, It.IsAny<CancellationToken>()),
            Times.Once);

        _appTester.VerifyAll();
    }

    [Fact]
    public async Task OrchestrateMacCatalystTestTest()
    {
        // Setup
        _appInstaller.Reset();
        _appUninstaller.Reset();
        _deviceFinder.Reset();

        var testTarget = new TestTargetOs(TestTarget.MacCatalyst, null);

        var envVars = new[] { ("envVar1", "value1"), ("envVar2", "value2") };

        _appTester
            .Setup(x => x.TestMacCatalystApp(
                _appBundleInformation,
                TimeSpan.FromMinutes(30),
                It.IsAny<TimeSpan>(),
                true,
                It.IsAny<IEnumerable<string>>(),
                envVars,
                It.IsAny<XmlResultJargon>(),
                It.IsAny<string[]?>(),
                It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.Failed, "Tests failed"))
            .Verifiable();

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            It.IsAny<TimeSpan>(),
            CommunicationChannel.UsbTunnel,
            XmlResultJargon.xUnit,
            Array.Empty<string>(),
            Array.Empty<string>(),
            includeWirelessDevices: false,
            resetSimulator: true,
            enableLldb: false,
            signalAppEnd: true,
            envVars,
            Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.TESTS_FAILED, result);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);

        _appTester.VerifyAll();
        _deviceFinder.VerifyNoOtherCalls();
        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }
}
