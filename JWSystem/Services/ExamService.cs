using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using JWSystem.Models;

namespace JWSystem.Services
{
    public class ExamService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _appSettings;

        public ExamService(AppSettings appSettings)
        {
            _appSettings = appSettings;
            var handler = new HttpClientHandler
            {
                CookieContainer = new System.Net.CookieContainer(),
                UseCookies = true,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true // Allow all certs
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Referer", _appSettings.BaseUrl + "/");
        }

        public string EncodeInp(string text)
        {
            const string keyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
            var output = new StringBuilder();
            
            var textBytes = Encoding.UTF8.GetBytes(text);
            int idx = 0;
            
            while (idx < textBytes.Length)
            {
                byte chr1 = textBytes[idx++];
                byte chr2 = idx < textBytes.Length ? textBytes[idx++] : (byte)0;
                byte chr3 = idx < textBytes.Length ? textBytes[idx++] : (byte)0;
                
                int enc1 = chr1 >> 2;
                int enc2 = ((chr1 & 3) << 4) | (chr2 >> 4);
                int enc3 = ((chr2 & 15) << 2) | (chr3 >> 6);
                int enc4 = chr3 & 63;
                
                if (chr2 == 0)
                {
                    enc3 = enc4 = 64;
                }
                else if (chr3 == 0)
                {
                    enc4 = 64;
                }
                
                output.Append(keyStr[enc1]);
                output.Append(keyStr[enc2]);
                output.Append(keyStr[enc3]);
                output.Append(keyStr[enc4]);
            }
            
            return output.ToString();
        }

        public async Task<bool> CheckLoginStatusAsync()
        {
            try
            {
                var mainPageUrl = $"{_appSettings.BaseUrl}/jsxsd/framework/xsMain.jsp";
                var response = await _httpClient.GetAsync(mainPageUrl);
                var content = await ReadContentAsync(response);
                return response.IsSuccessStatusCode && content.Contains("学生个人中心");
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var loginUrl = $"{_appSettings.BaseUrl}/jsxsd/xk/LoginToXk";
            
            var encodedUsername = EncodeInp(username);
            var encodedPassword = EncodeInp(password);
            var encoded = $"{encodedUsername}%%%{encodedPassword}";
            
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("encoded", encoded)
            });
            
            try
            {
                // 首先访问基础URL获取会话cookies
                await _httpClient.GetAsync($"{_appSettings.BaseUrl}/");
                
                var response = await _httpClient.PostAsync(loginUrl, formData);
                
                if (await CheckLoginStatusAsync())
                {
                    return true;
                }
                else
                {
                    var responseContent = await ReadContentAsync(response);
                    return false;
                }
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<string> ReadContentAsync(HttpResponseMessage response)
        {
            var charset = response.Content.Headers.ContentType?.CharSet;
            if (string.IsNullOrWhiteSpace(charset) || !IsSupportedEncoding(charset))
            {
                charset = "gb2312"; // Fallback to gb2312
            }

            var encoding = Encoding.GetEncoding(charset);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return encoding.GetString(bytes);
        }

        private bool IsSupportedEncoding(string charset)
        {
            try
            {
                Encoding.GetEncoding(charset);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public async Task<string?> GetExamPageAsync()
        {
            if (!await CheckLoginStatusAsync())
            {
                return null;
            }

            var examUrl = $"{_appSettings.BaseUrl}/jsxsd/xsks/xsksap_query";
            
            try
            {
                var response = await _httpClient.GetAsync(examUrl);
                response.EnsureSuccessStatusCode();

                var content = await ReadContentAsync(response);
                if (content.Contains("统一身份认证") || content.Contains("用户登录"))
                {
                    return null;
                }

                return content;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<string?> GetExamListAsync(string xnxqid = "2024-2025-2")
        {
            if (!await CheckLoginStatusAsync())
            {
                return null;
            }

            var examListUrl = $"{_appSettings.BaseUrl}/jsxsd/xsks/xsksap_list";
            
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("xnxqid", xnxqid)
            });
            
            try
            {
                var response = await _httpClient.PostAsync(examListUrl, formData);
                response.EnsureSuccessStatusCode();

                var content = await ReadContentAsync(response);
                if (content.Contains("统一身份认证") || content.Contains("用户登录"))
                {
                    return null;
                }

                return content;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public List<ExamInfo> ParseExamList(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                return new List<ExamInfo>();
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                
                var examTable = doc.DocumentNode.SelectSingleNode("//table[@id='dataList']");
                if (examTable == null)
                {
                    return new List<ExamInfo>();
                }

                var exams = new List<ExamInfo>();
                var rows = examTable.SelectNodes(".//tr")?.Skip(1); // 跳过表头
                
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        var cells = row.SelectNodes(".//td");
                        if (cells?.Count >= 9)
                        {
                            var examInfo = new ExamInfo
                            {
                                Index = cells[0].InnerText.Trim(),
                                ExamId = cells[1].InnerText.Trim(),
                                CourseCode = cells[2].InnerText.Trim(),
                                CourseName = cells[3].InnerText.Trim(),
                                ExamTime = cells[4].InnerText.Trim(),
                                ExamRoom = cells[5].InnerText.Trim(),
                                SeatNumber = cells[6].InnerText.Trim(),
                                ExamMethod = cells[7].InnerText.Trim(),
                                Remarks = cells[8].InnerText.Trim()
                            };
                            exams.Add(examInfo);
                        }
                    }
                }
                
                return exams;
            }
            catch (Exception)
            {
                return new List<ExamInfo>();
            }
        }

        public List<TermOption> GetTermOptions(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
                return new List<TermOption>();
                
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                
                var select = doc.DocumentNode.SelectSingleNode("//select[@id='xnxqid']");
                if (select == null)
                    return new List<TermOption>();
                    
                var options = new List<TermOption>();
                var optionNodes = select.SelectNodes(".//option");
                
                if (optionNodes != null)
                {
                    foreach (var option in optionNodes)
                    {
                        var value = option.GetAttributeValue("value", "");
                        var text = option.InnerText.Trim();
                        var selected = option.Attributes.Contains("selected");
                        
                        options.Add(new TermOption
                        {
                            Value = value,
                            Text = text,
                            Selected = selected
                        });
                    }
                }
                
                return options;
            }
            catch (Exception)
            {
                return new List<TermOption>();
            }
        }

        public ExamTimeInfo FormatExamTime(string timeStr)
        {
            try
            {
                var parts = timeStr.Split(' ');
                if (parts.Length == 2)
                {
                    var date = parts[0];
                    var times = parts[1].Split('~');
                    if (times.Length == 2)
                    {
                        return new ExamTimeInfo
                        {
                            Date = date,
                            StartTime = times[0],
                            EndTime = times[1],
                            Full = timeStr
                        };
                    }
                }
                
                return new ExamTimeInfo
                {
                    Date = "",
                    StartTime = "",
                    EndTime = "",
                    Full = timeStr
                };
            }
            catch
            {
                return new ExamTimeInfo
                {
                    Date = "",
                    StartTime = "",
                    EndTime = "",
                    Full = timeStr
                };
            }
        }

        public List<ExamInfo> SortExamsByDate(List<ExamInfo> exams)
        {
            if (!exams.Any())
                return exams;
                
            return exams.OrderBy(exam =>
            {
                var examTime = FormatExamTime(exam.ExamTime);
                if (!string.IsNullOrEmpty(examTime.Date))
                {
                    if (DateTime.TryParse(examTime.Date, out var date))
                        return date;
                }
                return DateTime.MaxValue;
            }).ToList();
        }

        public List<ExamInfo> GetUpcomingExams(List<ExamInfo> exams, int days = 7)
        {
            if (!exams.Any())
                return new List<ExamInfo>();
                
            var today = DateTime.Now;
            var upcoming = new List<ExamInfo>();
            
            foreach (var exam in exams)
            {
                var examTime = FormatExamTime(exam.ExamTime);
                if (!string.IsNullOrEmpty(examTime.Date))
                {
                    if (DateTime.TryParse(examTime.Date, out var examDate))
                    {
                        var daysUntil = (examDate - today).Days;
                        if (daysUntil >= 0 && daysUntil <= days)
                        {
                            exam.DaysUntil = daysUntil;
                            upcoming.Add(exam);
                        }
                    }
                }
            }
                    
            return upcoming;
        }

        public int? CountDaysUntilExam(string examDateStr)
        {
            if (DateTime.TryParse(examDateStr, out var examDate))
            {
                var today = DateTime.Now.Date;
                var delta = examDate - today;
                return delta.Days;
            }
            return null;
        }

        public async Task<bool> PushExamsAsync(List<ExamInfo> exams, string termName, string pushToken)
        {
            if (string.IsNullOrEmpty(pushToken) || !exams.Any())
            {
                return false;
            }
                
            try
            {
                var today = DateTime.Now;
                var dateStr = today.ToString("yyyy-MM-dd");
                
                var sortedExams = SortExamsByDate(exams);
                var upcomingExams = GetUpcomingExams(sortedExams);
                
                var content = BuildExamNotificationContent(exams, termName, upcomingExams, sortedExams);
                
                var pushData = new
                {
                    token = pushToken,
                    title = $"📝 {termName}考试安排 ({dateStr})",
                    content = content,
                    template = "html"
                };

                var json = JsonSerializer.Serialize(pushData);
                var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(_appSettings.PushPlusUrl, stringContent);
                var result = await ReadContentAsync(response);
                var resultData = JsonSerializer.Deserialize<JsonElement>(result);
                
                if (resultData.TryGetProperty("code", out var code) && code.GetInt32() == 200)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string BuildExamNotificationContent(List<ExamInfo> exams, string termName, List<ExamInfo> upcomingExams, List<ExamInfo> sortedExams)
        {
            var content = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px;"">
                <div style=""background-color: #f8f9fa; padding: 15px; border-radius: 8px; margin-bottom: 20px;"">
                    <h2 style=""color: #2c3e50; margin: 0; text-align: center;"">{termName}考试安排</h2>
                    <p style=""color: #7f8c8d; text-align: center; margin-top: 5px;"">共 {exams.Count} 门考试</p>
                </div>";
            
            // 如果有即将到来的考试，优先显示
            if (upcomingExams.Any())
            {
                content += @"
                <div style=""background-color: #fff3cd; padding: 10px; border-radius: 5px; margin-bottom: 20px; border-left: 4px solid #ffc107;"">
                    <h3 style=""color: #856404; margin-top: 0;"">⚠️ 近期考试提醒</h3>
                    <ul style=""padding-left: 20px;"">";
                
                foreach (var exam in upcomingExams)
                {
                    var examTime = FormatExamTime(exam.ExamTime);
                    var daysText = exam.DaysUntil == 0 ? "今天" : $"{exam.DaysUntil}天后";
                    content += $@"
                    <li style=""margin-bottom: 8px;"">
                        <span style=""font-weight: bold;"">{exam.CourseName}</span> - 
                        <span style=""color: #e74c3c;"">{examTime.Date} ({daysText})</span> 
                        <span>{examTime.StartTime}-{examTime.EndTime}</span>, 
                        <span>地点: {exam.ExamRoom}</span>
                    </li>";
                }
                
                content += @"
                    </ul>
                </div>";
            }
            
            // 所有考试的详细表格
            content += @"
                <table style=""width: 100%; border-collapse: collapse; margin-top: 20px; box-shadow: 0 2px 3px rgba(0,0,0,0.1);"">
                    <thead>
                        <tr style=""background-color: #4a90e2; color: white;"">
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">课程</th>
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">日期</th>
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">时间</th>
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">地点</th>
                            <th style=""padding: 12px; text-align: center; border: 1px solid #ddd;"">剩余天数</th>
                        </tr>
                    </thead>
                    <tbody>";
            
            for (int i = 0; i < sortedExams.Count; i++)
            {
                var exam = sortedExams[i];
                var examTime = FormatExamTime(exam.ExamTime);
                var daysUntil = CountDaysUntilExam(examTime.Date);
                
                var bgColor = "#ffffff";
                var daysText = "未知";
                var daysColor = "#666666";
                
                if (daysUntil.HasValue)
                {
                    if (daysUntil < 0)
                    {
                        bgColor = "#f1f1f1";
                        daysText = "已结束";
                        daysColor = "#999999";
                    }
                    else if (daysUntil == 0)
                    {
                        bgColor = "#fff3cd";
                        daysText = "今天";
                        daysColor = "#e74c3c";
                    }
                    else if (daysUntil <= 7)
                    {
                        bgColor = "#fcf8e3";
                        daysText = $"{daysUntil}天";
                        daysColor = "#e67e22";
                    }
                    else
                    {
                        daysText = $"{daysUntil}天";
                        bgColor = i % 2 == 0 ? "#ffffff" : "#f8f9fa";
                    }
                }
                else
                {
                    bgColor = i % 2 == 0 ? "#ffffff" : "#f8f9fa";
                }
                
                content += $@"
                    <tr style=""background-color: {bgColor};"">
                        <td style=""padding: 12px; border: 1px solid #ddd;"">
                            <div style=""font-weight: bold;"">{exam.CourseName}</div>
                            <div style=""font-size: 12px; color: #666;"">{exam.CourseCode}</div>
                        </td>
                        <td style=""padding: 12px; border: 1px solid #ddd;"">{examTime.Date}</td>
                        <td style=""padding: 12px; border: 1px solid #ddd;"">{examTime.StartTime}~{examTime.EndTime}</td>
                        <td style=""padding: 12px; border: 1px solid #ddd;"">{exam.ExamRoom}</td>
                        <td style=""padding: 12px; border: 1px solid #ddd; text-align: center; color: {daysColor}; font-weight: bold;"">{daysText}</td>
                    </tr>";
            }
            
            content += @"
                    </tbody>
                </table>
                <div style=""margin-top: 20px; text-align: center; color: #666; font-size: 12px;"">
                    <p>考试安排可能随时变动，请以教务系统公告为准</p>
                    <p>此消息由教务系统自动推送</p>
                </div>
            </div>";

            return content;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class ExamInfo
    {
        public string Index { get; set; } = "";
        public string ExamId { get; set; } = "";
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string ExamTime { get; set; } = "";
        public string ExamRoom { get; set; } = "";
        public string SeatNumber { get; set; } = "";
        public string ExamMethod { get; set; } = "";
        public string Remarks { get; set; } = "";
        public int DaysUntil { get; set; }
    }

    public class TermOption
    {
        public string Value { get; set; } = "";
        public string Text { get; set; } = "";
        public bool Selected { get; set; }
    }

    public class ExamTimeInfo
    {
        public string Date { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string Full { get; set; } = "";
    }
}
