using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Utils;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Enliven.Tests;

public class SingleTaskTests : IDisposable {
    private readonly List<int?> _forcedParams = [];
    private readonly SingleTask<int, int?> _singleTask;
    private int _timesExecuted;

    public SingleTaskTests() {
        _singleTask = new SingleTask<int, int?>(async data => {
            await Task.Delay(50);
            _forcedParams.Add(data.Parameter);
            return _timesExecuted++;
        });
    }

    public void Dispose() {
        _singleTask?.Dispose();
    }

    [Fact]
    public void SingleTask_Ctor_VerifyExpectations() {
        _singleTask.IsDisposed.Should().BeFalse();
        _singleTask.IsExecuting.Should().BeFalse();
        _timesExecuted.Should().Be(0);
    }

    [Fact]
    public void SingleTask_TaskTaskAction_ThrowExceptionInCtor() {
        var action = () => Task.FromResult(Task.FromResult(0));
        Assert.Throws(typeof(InvalidOperationException), () => new SingleTask<Task<int>>(action));
    }

    [Fact]
    public async Task SingleTask_DirtyFirstSingleExecute_VerifyExpectations() {
        var expression = () => _singleTask.Execute(true, TimeSpan.Zero);

        await expression.Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        _timesExecuted.Should().Be(1);
    }

    [Fact]
    public async Task SingleTask_NonDirtyFirstSingleExecute_VerifyExpectations() {
        var expression = () => _singleTask.Execute(false, TimeSpan.Zero);

        await expression.Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        _timesExecuted.Should().Be(1);
    }

    [Fact]
    public async Task SingleTask_FirstExecutionDirty_ShouldStartExecution() {
        _singleTask.BetweenExecutionsDelay = TimeSpan.Zero;

        await GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        _timesExecuted.Should().Be(1);
    }

    [Fact]
    public async Task SingleTask_TwoNonDirtyExecutionsOneByOne_VerifyExpectations() {
        _singleTask.BetweenExecutionsDelay = TimeSpan.Zero;

        await GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        _timesExecuted.Should().Be(1);

        await GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        _timesExecuted.Should().Be(1);
    }

    [Fact]
    public async Task SingleTask_ShouldExecuteNonDirtyIfNothingRunningProperty_VerifyExpectations() {
        _singleTask.BetweenExecutionsDelay = TimeSpan.Zero;
        _singleTask.ShouldExecuteNonDirtyIfNothingRunning = true;

        await GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        _timesExecuted.Should().Be(1);

        await GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(1);
        _timesExecuted.Should().Be(2);
    }

    [Fact]
    public async Task SingleTask_TwoNonDirtyExecutionsSimultaneously_VerifyExpectations() {
        _singleTask.BetweenExecutionsDelay = TimeSpan.FromSeconds(1);

        var firstRun = GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        var secondRun = GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);

        await firstRun;
        await secondRun;

        _timesExecuted.Should().Be(1);
    }

    [Fact]
    public async Task SingleTask_ManyNonDirtyExecutionsSimultaneously_VerifyExpectations() {
        _singleTask.BetweenExecutionsDelay = TimeSpan.FromMilliseconds(150);

        var firstRun = GetExecuteExpression(true).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        var otherTasks = Enumerable.Range(0, 10)
            .Select(i => GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0))
            .ToArray();

        await firstRun;
        await Task.WhenAll(otherTasks);
        _timesExecuted.Should().Be(1);
    }

    [Fact]
    public async Task SingleTask_ManyNonDirtySplitByDirtyExecution_VerifyExpectations() {
        _singleTask.BetweenExecutionsDelay = TimeSpan.FromMilliseconds(150);

        var firstRun = GetExecuteExpression(true).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        var firstTasks = Enumerable.Range(0, 10)
            .Select(i => GetExecuteExpression(false).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0))
            .ToArray();

        var secondRun = GetExecuteExpression(true).Should().CompleteWithinAsync(300.Milliseconds()).WithResult(1);
        var secondTasks = Enumerable.Range(0, 10)
            .Select(i => GetExecuteExpression(false).Should().CompleteWithinAsync(300.Milliseconds()).WithResult(1))
            .ToArray();

        await firstRun;
        await Task.WhenAll(firstTasks);

        await secondRun;
        await Task.WhenAll(secondTasks);

        _timesExecuted.Should().Be(2);
    }

    [Fact]
    public async Task SingleTask_ManyDirtySplit_VerifyExpectations() {
        _singleTask.BetweenExecutionsDelay = TimeSpan.FromMilliseconds(150);

        var firstRun = GetExecuteExpression(true).Should().CompleteWithinAsync(100.Milliseconds()).WithResult(0);
        var secondTasks = Enumerable.Range(0, 10)
            .Select(i => GetExecuteExpression(true).Should().CompleteWithinAsync(300.Milliseconds()).WithResult(1))
            .ToArray();

        await firstRun;
        await Task.WhenAll(secondTasks);

        _timesExecuted.Should().Be(2);
    }

    [Fact]
    public async Task SingleTask_ManyForced_VerifyExpectations() {
        await Enumerable.Range(0, 10)
            .Select(i => _ = _singleTask.ForcedExecute(i))
            .WhenAll();
        _forcedParams.Should().BeEquivalentTo(Enumerable.Range(0, 10));
    }

    [Fact]
    public void SingleTask_Dispose_VerifyExpectations() {
        _singleTask.Dispose();

        _singleTask.IsDisposed.Should().BeTrue();
        _singleTask.IsExecuting.Should().BeFalse();

        Action execAction = () => _singleTask.Execute();

        execAction.Should().ThrowExactly<ObjectDisposedException>();
        _timesExecuted.Should().Be(0);
    }

    private Func<Task<int>> GetExecuteExpression(bool makesDirty, TimeSpan? delayOverride = null) {
        return () => _singleTask.Execute(makesDirty, delayOverride);
    }
}