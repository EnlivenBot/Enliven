using System;
using System.Threading.Tasks;
using Common.Utils;
using FluentAssertions;
using Xunit;

namespace Enliven.Tests {
    public class HandyTimerTests : IDisposable {
        private HandyTimer _handyTimer;
        public HandyTimerTests() {
            _handyTimer = new HandyTimer();
        }
        
        [Fact]
        public void HandyTimer_Ctor_VerifyExpectations() {
            _handyTimer.IsDisposed.Should().BeFalse();
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }
        
        [Fact]
        public void HandyTimer_FirstNegativeDelay_ShouldBeElapsed() {
            var taskBefore = _handyTimer.TimerElapsed;
            
            _handyTimer.SetDelay(TimeSpan.FromMinutes(-1));

            _handyTimer.TimerElapsed.Should().BeSameAs(taskBefore);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }
        
        [Fact]
        public void HandyTimer_SecondNegativeDelay_VerifyExpectations() {
            _handyTimer.SetDelay(TimeSpan.FromMinutes(1));
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();
            
            _handyTimer.SetDelay(TimeSpan.FromMinutes(-1));
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }
        
        [Fact]
        public async Task HandyTimer_OneValue_VerifyExpectations() {
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(200));
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();
            
            await Task.Delay(200);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }
        
        [Fact]
        public async Task HandyTimer_TwoValuesGreater_AwaitingForGreater() {
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(200));
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(400));
            
            await Task.Delay(220);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();
            
            await Task.Delay(220);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }
        
        [Fact]
        public async Task HandyTimer_TwoValuesSmaller_ShouldUseSmaller() {
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(400));
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(200));
            
            await Task.Delay(220);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }
        
        [Fact]
        public void HandyTimer_Dispose_ShouldThrowDisposed() {
            _handyTimer.Dispose();

            _handyTimer.IsDisposed.Should().BeTrue();
            Action setDelayAction = () => _handyTimer.SetDelay(TimeSpan.Zero);
            Action setTargetTimeAction = () => _handyTimer.SetTargetTime(DateTime.Now);
            setDelayAction.Should().ThrowExactly<ObjectDisposedException>();
            setTargetTimeAction.Should().ThrowExactly<ObjectDisposedException>();
        }
        
        public void Dispose() {
            _handyTimer?.Dispose();
        }
    }
}