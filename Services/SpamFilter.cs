using System;
using System.Collections.Generic;
using NetworkMonitor.Objects;
using System.Linq;
using MetroLog;
namespace NetworkMonitor.Service.Services
{
    public class SpamFilter
    {
        private List<UserEmailSent> _userEmailSentList = new List<UserEmailSent>();
        private TimeSpan allowedTimeLimitVerify = new TimeSpan(1, 0, 0);
        private int maxVerify = 10;
        private ILogger _logger;
        public SpamFilter(ILogger logger)
        {
            _logger = logger;
        }
        public void UpdateAlertSentList(AlertMessage alertMessage)
        {
            var userEmailSent = new UserEmailSent();
            userEmailSent.UserID = alertMessage.UserInfo.UserID;
            userEmailSent.DateSent = DateTime.UtcNow;
            if (alertMessage.VerifyLink)
            {
                userEmailSent.IsVerifyEmail = true;
                userEmailSent.IsAlertEmail = false;
            }
            else
            {
                userEmailSent.IsVerifyEmail = false;
                userEmailSent.IsAlertEmail = true;
            }
            _userEmailSentList.Add(userEmailSent);
        }
        public ResultObj IsVerifyLimit(string userID)
        {
            var result = new ResultObj();
            result.Success = true;
            List<UserEmailSent> userEmailSentList = _userEmailSentList.Where(w => w.UserID == userID && w.IsVerifyEmail == true).OrderBy(o => o.DateSent).ToList();
            if (userEmailSentList.Count() > maxVerify)
            {
                result.Success = false;
                result.Message = "You have sent " + maxVerify + " requests to send a verify email. Please check your email. If the email is not working then contact support.";
                _logger.Warn("Warning filter applied for maxVerify for user " + userID);
                return result;
            }
            if (userEmailSentList.Count() > 0 && userEmailSentList[0].DateSent + allowedTimeLimitVerify > DateTime.UtcNow)
            {
                result.Success = false;
                result.Message = "Please check your email a verfication email was sent less an hour ago. If problem the presists then contact support.";
                _logger.Warn("Warning filter applied for allowedTimeLimitVerify for user " + userID);
                return result;
            }
            _logger.Debug("CheckVerifyLimit passed for user " + userID);
            return result;
        }
    }
}