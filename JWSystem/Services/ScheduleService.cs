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
    public class ScheduleService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _appSettings;
        private readonly DateTime _firstWeekMonday = new DateTime(2025, 3, 3);

        public ScheduleService(AppSettings appSettings)
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

        public int GetCurrentWeek()
        {
            var today = DateTime.Now;
            var daysDiff = (today - _firstWeekMonday).Days;
            var currentWeek = (daysDiff / 7) + 1;
            return Math.Max(1, currentWeek);
        }

        public string EncodeInp(string text)
        {
            const string keyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
            var output = new StringBuilder();
            
            int i = 0;
            
            while (i < text.Length)
            {
                int chr1 = text[i++];
                int chr2 = i < text.Length ? text[i++] : 0;
                int chr3 = i < text.Length ? text[i++] : 0;
                
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
                var mainPageUrl = $"{_appSettings.BaseUrl}/framework/xsMain.jsp";
                var response = await _httpClient.GetAsync(mainPageUrl);
                var content = await response.Content.ReadAsStringAsync();
                return response.IsSuccessStatusCode && content.Contains("姓名：");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查登录状态时发生错误: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var loginUrl = $"{_appSettings.BaseUrl}{_appSettings.LoginPath}";
            
            var encodedUsername = EncodeInp(username);
            var encodedPassword = EncodeInp(password);
            var encoded = $"{encodedUsername}%%%{encodedPassword}";
            
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("encoded", encoded)
            });
            
            try
            {
                // 首先访问登录页面获取cookie
                await _httpClient.GetAsync($"{_appSettings.BaseUrl}/");
                
                // 发送登录请求
                var response = await _httpClient.PostAsync(loginUrl, formData);
                
                Console.WriteLine($"登录请求状态码: {response.StatusCode}");
                
                // 检查登录状态
                if (await CheckLoginStatusAsync())
                {
                    Console.WriteLine("登录成功！");
                    return true;
                }
                else
                {
                    Console.WriteLine("登录失败，请检查用户名和密码！");
                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (responseContent.Contains("验证码"))
                        Console.WriteLine("需要验证码，请稍后添加验证码处理功能");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录过程中发生错误: {ex.Message}");
                return false;
            }
        }

        public CourseInfo? ParseCourseInfo(HtmlNode cell)
        {
            try
            {
                var courseDiv = cell.SelectSingleNode(".//div[@class='kbcontent1']");
                if (courseDiv == null)
                    return null;
                
                var courseText = courseDiv.InnerText?.Trim();
                if (string.IsNullOrEmpty(courseText) || courseText == "\u00A0")
                    return null;
                
                var infoParts = courseText.Split('\n');
                if (!infoParts.Any())
                    return null;
                
                var courseInfo = new CourseInfo();
                
                if (infoParts.Length > 0)
                {
                    var fullText = infoParts[0].Trim();
                    
                    // 提取课程号
                    var courseCodeMatch = Regex.Match(fullText, @"\d{6}[A-Z]\d{3}-\d{2}");
                    if (courseCodeMatch.Success)
                    {
                        courseInfo.CourseCode = courseCodeMatch.Value;
                        fullText = fullText.Replace(courseInfo.CourseCode, "").Trim();
                    }
                    
                    // 提取周次信息
                    var weeksMatch = Regex.Match(fullText, @"\d+-\d+\(周\)");
                    if (weeksMatch.Success)
                    {
                        courseInfo.Weeks = weeksMatch.Value;
                        fullText = fullText.Replace(courseInfo.Weeks, "").Trim();
                    }
                    
                    // 处理课程名称和教室
                    var buildingMatch = Regex.Match(fullText, @"[A-Z]\d+楼");
                    if (buildingMatch.Success)
                    {
                        courseInfo.Name = fullText.Substring(0, buildingMatch.Index).Trim();
                        courseInfo.Classroom = fullText.Substring(buildingMatch.Index).Trim();
                    }
                    else
                    {
                        courseInfo.Name = fullText;
                    }
                }
                
                return courseInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析课程信息时出错: {ex.Message}");
                return null;
            }
        }

        public async Task<ScheduleData?> GetScheduleAsync()
        {
            try
            {
                var currentWeek = GetCurrentWeek();
                Console.WriteLine($"正在获取第{currentWeek}周的课表...");
                
                var scheduleUrl = $"{_appSettings.BaseUrl}/xskb/xskb_list.do";
                var queryParams = new List<KeyValuePair<string, string>>
                {
                    new("Ves632DSdyV", "NEW_XSD_PYGL"),
                    new("zc1", currentWeek.ToString()),
                    new("zc2", currentWeek.ToString()),
                    new("xnxq01id", "2024-2025-2")
                };
                
                var query = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));
                var fullUrl = $"{scheduleUrl}?{query}";
                
                var response = await _httpClient.GetAsync(fullUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var doc = new HtmlDocument();
                    doc.LoadHtml(content);
                    
                    var scheduleData = new List<ScheduleItem>();
                    var table = doc.DocumentNode.SelectSingleNode("//table[@id='kbtable']");
                    
                    if (table == null)
                    {
                        Console.WriteLine("未找到课表数据");
                        return null;
                    }
                    
                    var rows = table.SelectNodes(".//tr");
                    if (rows == null || rows.Count <= 1)
                    {
                        Console.WriteLine("课表数据为空");
                        return null;
                    }
                    
                    // 处理每一行（跳过表头）
                    foreach (var row in rows.Skip(1))
                    {
                        try
                        {
                            var cells = row.SelectNodes(".//th | .//td");
                            if (cells == null || cells.Count < 8)
                                continue;
                            
                            var timeSlot = cells[0].InnerText?.Trim();
                            
                            // 处理周一到周日的课程
                            for (int i = 1; i < 8; i++)
                            {
                                if (i < cells.Count)
                                {
                                    var courseInfo = ParseCourseInfo(cells[i]);
                                    if (courseInfo != null)
                                    {
                                        scheduleData.Add(new ScheduleItem
                                        {
                                            Time = timeSlot ?? "",
                                            Day = i,
                                            Course = courseInfo
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"处理行数据时出错: {ex.Message}");
                            continue;
                        }
                    }
                    
                    if (!scheduleData.Any())
                    {
                        Console.WriteLine("本周没有课程安排");
                        return null;
                    }
                    
                    return new ScheduleData
                    {
                        CurrentWeek = currentWeek,
                        Schedule = scheduleData
                    };
                }
                else
                {
                    Console.WriteLine($"获取课表失败，状态码：{response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取课表时发生错误: {ex.Message}");
                return null;
            }
        }

        public string ConvertTime(string timeCode)
        {
            var timeMap = new Dictionary<string, string>
            {
                {"0102", "9:30-11:05"},
                {"0304", "11:20-12:55"},
                {"0405", "12:10-13:45"},
                {"0607", "16:00-17:35"},
                {"0809", "17:50-19:25"},
                {"030405", "11:20-13:45"}
            };
            
            return timeMap.TryGetValue(timeCode, out var time) ? time : timeCode;
        }

        public async Task PushScheduleAsync(ScheduleData schedule, string pushToken)
        {
            if (string.IsNullOrEmpty(pushToken))
            {
                Console.WriteLine("PushToken为空，无法推送课表。");
                return;
            }
            try
            {
                var now = DateTime.Now;
                var isBefore8PM = now.Hour < 20;
                
                var targetDate = isBefore8PM ? now : now.AddDays(1);
                var weekday = (int)targetDate.DayOfWeek;
                if (weekday == 0) weekday = 7; // 周日转换为7
                
                var dateStr = targetDate.ToString("yyyy-MM-dd");
                
                var content = BuildScheduleContent(schedule, targetDate, weekday, dateStr);
                
                var pushData = new
                {
                    token = pushToken,
                    title = $"📚 {dateStr} 课表",
                    content = content,
                    template = "html"
                };

                var json = JsonSerializer.Serialize(pushData);
                var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(_appSettings.PushPlusUrl, stringContent);
                var result = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"PushPlus API Status Code: {response.StatusCode}");
                Console.WriteLine($"PushPlus API Response Text: {result}");
                
                if (!string.IsNullOrEmpty(result))
                {
                    try
                    {
                        var resultData = JsonSerializer.Deserialize<JsonElement>(result);
                        if (resultData.TryGetProperty("code", out var code) && code.GetInt32() == 200)
                        {
                            Console.WriteLine("课表推送成功！");
                        }
                        else
                        {
                            var msg = resultData.TryGetProperty("msg", out var msgElement) ? msgElement.GetString() : "未知错误";
                            Console.WriteLine($"课表推送失败：{msg}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"课表推送失败：无法解析 PushPlus API 的响应为 JSON。错误信息: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("课表推送失败：PushPlus API 返回了空响应。");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"推送课表时发生错误: {ex.Message}");
            }
        }

        private string BuildScheduleContent(ScheduleData schedule, DateTime targetDate, int weekday, string dateStr)
        {
            var content = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px;"">
                <div style=""background-color: #f8f9fa; padding: 15px; border-radius: 8px; margin-bottom: 20px;"">
                    <h2 style=""color: #2c3e50; margin: 0; text-align: center;"">{dateStr} 课表</h2>
                </div>
                <table style=""width: 100%; border-collapse: collapse; margin-top: 20px; box-shadow: 0 2px 3px rgba(0,0,0,0.1);"">
                    <thead>
                        <tr style=""background-color: #4a90e2; color: white;"">
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">时间</th>
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">星期</th>
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">课程</th>
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">周次</th>
                            <th style=""padding: 12px; text-align: left; border: 1px solid #ddd;"">教室</th>
                        </tr>
                    </thead>
                    <tbody>";

            var filteredSchedule = schedule.Schedule.Where(course => course.Day == weekday).ToList();
            
            var isToday = targetDate.Date == DateTime.Now.Date;
            Console.WriteLine($"\n--- {dateStr} 课表 ({(isToday ? "今天" : "明天")}) ---");
            
            if (!filteredSchedule.Any())
            {
                Console.WriteLine($"{dateStr} 没有课程安排");
                content += @"
                    <tr>
                        <td colspan=""5"" style=""padding: 15px; text-align: center; border: 1px solid #ddd; background-color: #f8f9fa;"">
                            <span style=""color: #666; font-style: italic;"">没有课程安排</span>
                        </td>
                    </tr>";
            }
            else
            {
                for (int i = 0; i < filteredSchedule.Count; i++)
                {
                    var course = filteredSchedule[i];
                    var courseInfo = course.Course;
                    
                    Console.WriteLine($"时间：{ConvertTime(course.Time)}, 星期{course.Day}");
                    Console.WriteLine($"课程名称：{courseInfo.Name}");
                    if (!string.IsNullOrEmpty(courseInfo.Weeks))
                        Console.WriteLine($"上课周次：{courseInfo.Weeks}");
                    if (!string.IsNullOrEmpty(courseInfo.Classroom))
                        Console.WriteLine($"上课教室：{courseInfo.Classroom}");
                    if (!string.IsNullOrEmpty(courseInfo.CourseCode))
                        Console.WriteLine($"课程编号：{courseInfo.CourseCode}");
                    Console.WriteLine(new string('-', 30));
                    
                    var bgColor = i % 2 == 0 ? "#ffffff" : "#f8f9fa";
                    content += $@"
                        <tr style=""background-color: {bgColor};"">
                            <td style=""padding: 12px; border: 1px solid #ddd;"">{ConvertTime(course.Time)}</td>
                            <td style=""padding: 12px; border: 1px solid #ddd;"">星期{course.Day}</td>
                            <td style=""padding: 12px; border: 1px solid #ddd; font-weight: bold;"">{courseInfo.Name}</td>
                            <td style=""padding: 12px; border: 1px solid #ddd;"">{courseInfo.Weeks}</td>
                            <td style=""padding: 12px; border: 1px solid #ddd;"">{courseInfo.Classroom}</td>
                        </tr>";
                }
            }
            
            Console.WriteLine("--- 课表结束 ---\n");
            
            content += @"
                    </tbody>
                </table>
                <div style=""margin-top: 20px; text-align: center; color: #666; font-size: 12px;"">
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

    public class ScheduleData
    {
        public int CurrentWeek { get; set; }
        public List<ScheduleItem> Schedule { get; set; } = new();
    }

    public class ScheduleItem
    {
        public string Time { get; set; } = "";
        public int Day { get; set; }
        public CourseInfo Course { get; set; } = new();
    }

    public class CourseInfo
    {
        public string Name { get; set; } = "";
        public string Weeks { get; set; } = "";
        public string Classroom { get; set; } = "";
        public string CourseCode { get; set; } = "";
    }
}
