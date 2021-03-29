using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IQFeed.CSharpApiClient.Lookup;
using NUnit.Framework;

namespace IQFeed.CSharpApiClient.Tests.Lookup
{
    /// <summary>
    /// Some tests have a tendency to be flaky under CI heavy load.
    /// </summary>
    public class LookupRateLimiterTests
    {
        [Test]
        public async Task Should_Rate_Limit_By_Throughput()
        {

            // Arrange
            var totalSeconds = TimeSpan.FromSeconds(10).TotalSeconds;
            var seconds = 0;

            var requestsPerSecond = 50;
            var requestsCount = 0;
            var testResults = new List<TestResult>();

            var lookupRateLimiter = new LookupRateLimiter(requestsPerSecond);
            var cts = new CancellationTokenSource();

            // Act
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await lookupRateLimiter.WaitAsync();
                    Interlocked.Increment(ref requestsCount);
                }
            }, cts.Token);

            while (seconds < totalSeconds)
            {
                var sw = Stopwatch.StartNew();
                await Task.Delay(TimeSpan.FromSeconds(1));
                sw.Stop();

                Console.WriteLine($"requests: {requestsCount}/{sw.Elapsed.TotalMilliseconds}ms");
                testResults.Add(new TestResult(requestsCount, sw.Elapsed.TotalMilliseconds));
                Interlocked.Exchange(ref requestsCount, 0);
                seconds++;
            }

            cts.Cancel();
            cts.Dispose();

            // Assert
            var totalMs = testResults.Sum(x => x.TotalMilliseconds);
            var totalRequests = testResults.Sum(x => x.NumberOfRequest);
            var calcRequestsPerSecond = totalRequests * 1000 / totalMs;
            var variationPercentage = Math.Abs(calcRequestsPerSecond - requestsPerSecond) / requestsPerSecond * 100;

            Console.WriteLine($"Average {calcRequestsPerSecond} requests/second");
            Console.WriteLine($"Variation {variationPercentage} %");
            
            Assert.That(calcRequestsPerSecond, Is.EqualTo(requestsPerSecond).Within(1).Percent);
        }

        [Test]
        public void Should_Validate_RequestsPerSecond()
        {
            // Arrange
            var requestsPerSecond = 50;

            // Act
            var lookupRateLimiter = new LookupRateLimiter(requestsPerSecond);

            // Assert
            Assert.AreEqual(lookupRateLimiter.RequestsPerSecond, requestsPerSecond);
        }

        [Test]
        public void Should_Validate_Interval()
        {
            // Arrange
            var requestsPerSecond = 50;

            // Act
            var lookupRateLimiter = new LookupRateLimiter(requestsPerSecond);

            // Assert
            var expectedInterval = TimeSpan.FromMilliseconds(1000 / requestsPerSecond);
            Assert.AreEqual(lookupRateLimiter.Interval, expectedInterval);
        }

        [Test]
        public void Should_Dispose()
        {
            // Arrange
            var requestsPerSecond = 50;
            var lookupRateLimiter = new LookupRateLimiter(requestsPerSecond);

            // Act
            lookupRateLimiter.Dispose();

            // Assert
            Assert.False(lookupRateLimiter.IsRunning);
        }

        class TestResult
        {
            public TestResult(int numberOfRequest, double totalMilliseconds)
            {
                NumberOfRequest = numberOfRequest;
                TotalMilliseconds = totalMilliseconds;
            }

            public int NumberOfRequest { get; }
            public double TotalMilliseconds { get; }
        }
    }
}