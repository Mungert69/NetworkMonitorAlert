using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkMonitor.Alert.Services;
using NetworkMonitor.Objects;
using Xunit;

namespace NetworkMonitorAlert.Tests.Services
{
    public class SpamFilterTests
    {
        private readonly Mock<ILogger> _loggerMock = new();

        private SpamFilter CreateFilter()
        {
            return new SpamFilter(_loggerMock.Object);
        }

        private AlertMessage CreateAlertMessage(string userId, bool verifyLink = false)
        {
            return new AlertMessage
            {
                UserInfo = new UserInfo { UserID = userId },
                VerifyLink = verifyLink
            };
        }

        [Fact]
        public void UpdateAlertSentList_AddsVerifyAndAlertEmails()
        {
            var filter = CreateFilter();
            var alertMsgVerify = CreateAlertMessage("user1", true);
            var alertMsgAlert = CreateAlertMessage("user1", false);

            filter.UpdateAlertSentList(alertMsgVerify);
            filter.UpdateAlertSentList(alertMsgAlert);

            // Use reflection to access the private _userEmailSentList for test verification
            var field = typeof(SpamFilter).GetField("_userEmailSentList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            var listObj = field.GetValue(filter);
            Assert.NotNull(listObj);
            var list = (List<UserEmailSent>)listObj;

            Assert.Equal(2, list.Count);
            Assert.True(list[0].IsVerifyEmail);
            Assert.False(list[0].IsAlertEmail);
            Assert.False(list[1].IsVerifyEmail);
            Assert.True(list[1].IsAlertEmail);
        }

        [Fact]
        public void IsVerifyLimit_ReturnsFalse_WhenOverMaxVerify()
        {
            var filter = CreateFilter();
            string userId = "user2";
            // Add 11 verify emails to exceed the maxVerify limit (10)
            for (int i = 0; i < 11; i++)
            {
                var msg = CreateAlertMessage(userId, true);
                filter.UpdateAlertSentList(msg);
            }

            var result = filter.IsVerifyLimit(userId);

            Assert.False(result.Success);
            Assert.Contains("You have sent 10 requests", result.Message);
        }

        [Fact]
        public void IsVerifyLimit_ReturnsFalse_WhenWithinTimeLimit()
        {
            var filter = CreateFilter();
            string userId = "user3";
            // Add one verify email with DateSent set to now
            var msg = CreateAlertMessage(userId, true);
            filter.UpdateAlertSentList(msg);

            // Manually set the DateSent to now for the first entry
            var field = typeof(SpamFilter).GetField("_userEmailSentList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            var listObj = field.GetValue(filter);
            Assert.NotNull(listObj);
            var list = (List<UserEmailSent>)listObj;
            list[0].DateSent = DateTime.UtcNow;

            var result = filter.IsVerifyLimit(userId);

            Assert.False(result.Success);
            Assert.Contains("less an hour ago", result.Message);
        }

        [Fact]
        public void IsVerifyLimit_ReturnsTrue_WhenUnderLimitAndOutsideTime()
        {
            var filter = CreateFilter();
            string userId = "user4";
            // Add one verify email with DateSent set to over an hour ago
            var msg = CreateAlertMessage(userId, true);
            filter.UpdateAlertSentList(msg);

            // Manually set the DateSent to 2 hours ago
            var field = typeof(SpamFilter).GetField("_userEmailSentList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            var listObj = field.GetValue(filter);
            Assert.NotNull(listObj);
            var list = (List<UserEmailSent>)listObj;
            list[0].DateSent = DateTime.UtcNow.AddHours(-2);

            var result = filter.IsVerifyLimit(userId);

            Assert.True(result.Success);
        }
    }
}
