namespace Bookify.Infrastructure.Tests.Outbox
{
    using Bookify.Infrastructure.Outbox;
    using FluentAssertions;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Quartz;
    using Quartz.Impl.Triggers;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class ProcessOutboxMessagesJobSetupTests
    {
        private const string JobName = nameof(ProcessOutboxMessagesJob);

        private readonly QuartzOptions quartzOptions = new();

        [TestMethod]
        [DataRow(1)]
        [DataRow(10)]
        [DataRow(60)]
        public void Configure_Should_RegisterNonConcurrentJobAndRepeatingTrigger(int intervalInSeconds)
        {
            // Arrange

            ProcessOutboxMessagesJobSetup setup = CreateSetup(intervalInSeconds);

            // Act

            setup.Configure(quartzOptions);

            // Assert

            IJobDetail job = quartzOptions.JobDetails.Should().ContainSingle().Subject;

            job.Key.Should().Be(new JobKey(JobName));

            job.JobType.Should().Be(typeof(ProcessOutboxMessagesJob));

            job.ConcurrentExecutionDisallowed.Should().BeTrue();

            ITrigger trigger = quartzOptions.Triggers.Should().ContainSingle().Subject;

            trigger.JobKey.Should().Be(job.Key);

            ISimpleTrigger schedule = trigger.Should().BeAssignableTo<ISimpleTrigger>().Subject;

            schedule.RepeatInterval.Should().Be(TimeSpan.FromSeconds(intervalInSeconds));

            schedule.RepeatCount.Should().Be(SimpleTriggerImpl.RepeatIndefinitely);

            schedule.EndTimeUtc.Should().BeNull();
        }

        private static ProcessOutboxMessagesJobSetup CreateSetup(int intervalInSeconds) => new(
            Options.Create(new OutboxOptions { IntervalInSeconds = intervalInSeconds }));
    }
}
