using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using HtmlAgilityPack;
using System.IO;
using System.Text.RegularExpressions;
using JWSystem.Models;

namespace JWSystem.Services
{
    public class GradeService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _appSettings;
        private readonly string _previousGradesFile = "previous_grades_data.json";

        public GradeService(AppSettings appSettings)
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
                await _httpClient.GetAsync($"{_appSettings.BaseUrl}/jsxsd/");
                
                var response = await _httpClient.PostAsync(loginUrl, formData);
                
                // 检查登录状态
                if (await CheckLoginStatusAsync())
                {
                    Console.WriteLine("登录成功！");
                    return true;
                }
                else
                {
                    Console.WriteLine("登录失败。");
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (responseContent.Contains("验证码"))
                        Console.WriteLine("登录失败，可能需要验证码。请检查教务系统登录页面。");
                    else if (responseContent.Contains("用户名或密码错误") || responseContent.Contains("密码不正确") || responseContent.Contains("用户名不存在"))
                        Console.WriteLine("登录失败，用户名或密码错误。");
                    else
                        Console.WriteLine("登录失败，未知错误。请检查网络或教务系统状态。");
                    
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"登录过程中发生网络错误: {ex.Message}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"登录过程中发生超时错误: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录过程中发生未知错误: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CheckLoginStatusAsync()
        {
            try
            {
                var mainPageUrl = $"{_appSettings.BaseUrl}/jsxsd/framework/xsMain.jsp";
                var response = await _httpClient.GetAsync(mainPageUrl);
                var content = await response.Content.ReadAsStringAsync();
                
                // 检查页面是否包含学生个人中心内容，这表示已登录
                return response.IsSuccessStatusCode && content.Contains("学生个人中心");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查登录状态时发生错误: {ex.Message}");
                return false;
            }
        }

        public async Task<GradesData?> GetGradesAsync()
        {
            if (!await CheckLoginStatusAsync())
            {
                Console.WriteLine("用户未登录或会话已过期。");
                return null;
            }

            var gradesUrl = $"{_appSettings.BaseUrl}/jsxsd/kscj/cjcx_list?Ves632DSdyV=NEW_XSD_XJCJ";
            
            try
            {
                var response = await _httpClient.GetAsync(gradesUrl);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                if (content.Contains("统一身份认证") || content.Contains("用户登录"))
                {
                    Console.WriteLine("会话可能已过期或重定向到登录页。请尝试重新运行脚本。");
                    return null;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(content);
                
                var table = doc.DocumentNode.SelectSingleNode("//table[@id='dataList']");
                if (table == null)
                {
                    Console.WriteLine("未找到常规成绩数据表。HTML内容可能已更改或非预期。");
                    return null;
                }

                var gradesData = new GradesData { RegularGrades = new List<Grade>() };
                var rows = table.SelectNodes(".//tr")?.Skip(1); // 跳过表头
                
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        var cols = row.SelectNodes(".//td");
                        if (cols?.Count == 14)
                        {
                            var grade = new Grade
                            {
                                Index = cols[0].InnerText.Trim(),
                                Semester = cols[1].InnerText.Trim(),
                                CourseCode = cols[2].InnerText.Trim(),
                                CourseName = cols[3].InnerText.Trim(),
                                Score = cols[4].InnerText.Trim(),
                                Credit = cols[5].InnerText.Trim(),
                                TotalHours = cols[6].InnerText.Trim(),
                                GPA = cols[7].InnerText.Trim(),
                                AssessmentMethod = cols[8].InnerText.Trim(),
                                CourseAttribute = cols[9].InnerText.Trim(),
                                CourseNature = cols[10].InnerText.Trim(),
                                ExamNature = cols[11].InnerText.Trim(),
                                RetakeSemester = cols[12].InnerText.Trim(),
                                ScoreFlag = cols[13].InnerText.Trim()
                            };
                            gradesData.RegularGrades.Add(grade);
                        }
                    }
                }
                
                if (!gradesData.RegularGrades.Any())
                {
                    Console.WriteLine("常规成绩数据为空或未能解析。");
                    return null;
                }
                
                return gradesData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取成绩时发生错误: {ex.Message}");
                return null;
            }
        }

        public GradesData LoadPreviousGrades()
        {
            try
            {
                if (File.Exists(_previousGradesFile))
                {
                    Console.WriteLine($"从 {_previousGradesFile} 加载先前成绩...");
                    var json = File.ReadAllText(_previousGradesFile, Encoding.UTF8);
                    return JsonSerializer.Deserialize<GradesData>(json) ?? new GradesData { RegularGrades = new List<Grade>() };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载先前成绩时出错: {ex.Message}");
            }
            return new GradesData { RegularGrades = new List<Grade>() };
        }

        public void SaveGrades(GradesData gradesData)
        {
            try
            {
                var json = JsonSerializer.Serialize(gradesData, new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                });
                File.WriteAllText(_previousGradesFile, json, Encoding.UTF8);
                Console.WriteLine($"当前成绩已保存到 {_previousGradesFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存当前成绩时出错: {ex.Message}");
            }
        }

        public bool CompareGrades(List<Grade> currentGrades, List<Grade> previousGrades)
        {
            if (!previousGrades.Any() && currentGrades.Any())
                return true;
            
            if (currentGrades.Count != previousGrades.Count)
                return true;

            // 创建规范表示形式进行比较
            var currentCanonical = currentGrades.Select(g => GetCanonicalGrade(g)).OrderBy(x => x).ToList();
            var previousCanonical = previousGrades.Select(g => GetCanonicalGrade(g)).OrderBy(x => x).ToList();

            return !currentCanonical.SequenceEqual(previousCanonical);
        }

        private string GetCanonicalGrade(Grade grade)
        {
            return $"{grade.Semester}|{grade.CourseCode}|{grade.CourseName}|{grade.Score}|{grade.Credit}|{grade.GPA}|{grade.AssessmentMethod}|{grade.CourseAttribute}|{grade.CourseNature}|{grade.ExamNature}|{grade.RetakeSemester}|{grade.ScoreFlag}";
        }

        public async Task PushGradesNotificationAsync(GradesData gradesData, string username, string pushToken)
        {
            if (string.IsNullOrEmpty(pushToken) || gradesData?.RegularGrades == null || !gradesData.RegularGrades.Any())
            {
                Console.WriteLine("没有常规成绩数据可推送。");
                return;
            }

            try
            {
                var todayDate = DateTime.Now.ToString("yyyy-MM-dd");
                var userInfo = !string.IsNullOrEmpty(username) ? $"学号 {username} 的" : "";
                var title = $"📚 {userInfo}成绩通知 - {todayDate}";

                var content = BuildGradeNotificationContent(gradesData, userInfo);
                
                var pushData = new
                {
                    token = pushToken,
                    title = title,
                    content = content,
                    template = "html"
                };

                var json = JsonSerializer.Serialize(pushData);
                var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(_appSettings.PushPlusUrl, stringContent);
                var result = await response.Content.ReadAsStringAsync();
                var resultData = JsonSerializer.Deserialize<JsonElement>(result);
                
                if (resultData.TryGetProperty("code", out var code) && code.GetInt32() == 200)
                {
                    Console.WriteLine("成绩推送成功！");
                }
                else
                {
                    var msg = resultData.TryGetProperty("msg", out var msgElement) ? msgElement.GetString() : "未知错误";
                    Console.WriteLine($"成绩推送失败：{msg} (Code: {code.GetInt32()})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"推送成绩时发生错误: {ex.Message}");
            }
        }

        private string BuildGradeNotificationContent(GradesData gradesData, string userInfo)
        {
            const string tableFontSize = "12px";
            const string cellPadding = "5px";
            const string h2FontSize = "20px";
            const string h3FontSize = "16px";
            const string footerFontSize = "11px";

            var content = $@"
            <div style=""font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; max-width: 1000px; margin: 20px auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #f9f9f9;"">
                <div style=""background-color: #007bff; color: white; padding: 15px; border-radius: 8px 8px 0 0; margin: -20px -20px 20px -20px;"">
                    <h2 style=""margin: 0; text-align: center; font-size: {h2FontSize};"">{userInfo}个人成绩单</h2>
                </div>
                <h3 style=""color: #333; margin-top: 20px; margin-bottom: 8px; border-bottom: 2px solid #007bff; padding-bottom: 4px; font-size: {h3FontSize};"">详细成绩</h3>
                <table style=""width: 100%; border-collapse: collapse; margin-top: 8px; box-shadow: 0 1px 2px rgba(0,0,0,0.05); font-size: {tableFontSize};"">
                    <thead>
                        <tr style=""background-color: #f0f0f0; color: #333; font-weight: bold;"">
                            <th style=""padding: {cellPadding}; text-align: left; border: 1px solid #ddd;"">序号</th>
                            <th style=""padding: {cellPadding}; text-align: left; border: 1px solid #ddd;"">开课学期</th>
                            <th style=""padding: {cellPadding}; text-align: left; border: 1px solid #ddd;"">课程名称</th>
                            <th style=""padding: {cellPadding}; text-align: center; border: 1px solid #ddd;"">成绩</th>
                            <th style=""padding: {cellPadding}; text-align: center; border: 1px solid #ddd;"">学分</th>
                            <th style=""padding: {cellPadding}; text-align: center; border: 1px solid #ddd;"">绩点</th>
                            <th style=""padding: {cellPadding}; text-align: left; border: 1px solid #ddd;"">课程属性</th>
                            <th style=""padding: {cellPadding}; text-align: left; border: 1px solid #ddd;"">考试性质</th>
                        </tr>
                    </thead>
                    <tbody>";

            for (int i = 0; i < gradesData.RegularGrades.Count; i++)
            {
                var grade = gradesData.RegularGrades[i];
                var bgColor = i % 2 == 0 ? "#ffffff" : "#f7f7f7";
                var scoreStyle = "";
                
                if (int.TryParse(grade.Score, out int numericScore))
                {
                    if (numericScore < 60)
                        scoreStyle = "font-weight: bold; color: #d9534f;";
                    else if (numericScore >= 90)
                        scoreStyle = "font-weight: bold; color: #5cb85c;";
                }

                content += $@"
                        <tr style=""background-color: {bgColor};"">
                            <td style=""padding: {cellPadding}; border: 1px solid #ddd;"">{grade.Index}</td>
                            <td style=""padding: {cellPadding}; border: 1px solid #ddd;"">{grade.Semester}</td>
                            <td style=""padding: {cellPadding}; border: 1px solid #ddd; font-weight: bold;"">{grade.CourseName} ({grade.CourseCode})</td>
                            <td style=""padding: {cellPadding}; border: 1px solid #ddd; text-align: center; {scoreStyle}"">{grade.Score}</td>
                            <td style=""padding: {cellPadding}; border: 1px solid #ddd; text-align: center;"">{grade.Credit}</td>
                            <td style=""padding: {cellPadding}; border: 1px solid #ddd; text-align: center;"">{(string.IsNullOrEmpty(grade.GPA) ? "-" : grade.GPA)}</td>
                            <td style=""padding: {cellPadding}; border: 1px solid #ddd;"">{grade.CourseAttribute}</td>
                            <td style=""padding: {cellPadding}; border: 1px solid #ddd;"">{grade.ExamNature}</td>
                        </tr>";
            }

            content += $@"
                    </tbody>
                </table>
                <div style=""margin-top: 25px; text-align: center; color: #777; font-size: {footerFontSize};"">
                    <p>数据获取时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
                    <p>此消息由教务助手自动推送</p>
                </div>
            </div>";

            return content;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class GradesData
    {
        public List<Grade> RegularGrades { get; set; } = new();
    }

    public class Grade
    {
        public string Index { get; set; } = "";
        public string Semester { get; set; } = "";
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string Score { get; set; } = "";
        public string Credit { get; set; } = "";
        public string TotalHours { get; set; } = "";
        public string GPA { get; set; } = "";
        public string AssessmentMethod { get; set; } = "";
        public string CourseAttribute { get; set; } = "";
        public string CourseNature { get; set; } = "";
        public string ExamNature { get; set; } = "";
        public string RetakeSemester { get; set; } = "";
        public string ScoreFlag { get; set; } = "";
    }
}
