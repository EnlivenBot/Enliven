using System;
using System.Threading.Tasks;
using Common.Utils;
using FluentAssertions;
using Xunit;

namespace Enliven.Tests {
    public class HandyLongestTimerTests : IDisposable {
        private HandyLongestTimer _handyTimer;
        public HandyLongestTimerTests() {
            _handyTimer = new HandyLongestTimer();
        }
        
        [Fact]
        public void HandyLongestTimer_Ctor_VerifyExpectations() {
            _handyTimer.IsDisposed.Should().BeFalse();
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }
        
        [Fact]
        public async Task HandyLongestTimer_TwoValuesGreater_AwaitingForGreater() {
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(200));
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(400));
            
            await Task.Delay(220);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();
            
            await Task.Delay(220);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }
        
        [Fact]
        public async Task HandyLongestTimer_TwoValuesSmaller_ShouldUseSmaller() {
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(400));
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();
            _handyTimer.SetDelay(TimeSpan.FromMilliseconds(200));
            
            await Task.Delay(250);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeFalse();

            await Task.Delay(250);
            _handyTimer.TimerElapsed.IsCompleted.Should().BeTrue();
        }

        public void Dispose() {
            _handyTimer?.Dispose();
        }
    }
}