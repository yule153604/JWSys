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
    public class EvaluationService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _appSettings;

        public EvaluationService(AppSettings appSettings)
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
                Console.WriteLine($"正在检查登录状态，访问: {mainPageUrl}");
                
                var response = await _httpClient.GetAsync(mainPageUrl);
                Console.WriteLine($"主页响应状态: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"访问主页失败: {response.StatusCode}");
                    return false;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"主页内容长度: {content.Length} 字符");
                
                // 检查是否包含学生个人中心，这是登录成功的标志
                bool hasStudentCenter = content.Contains("学生个人中心");
                bool hasLogin = content.Contains("用户登录") || content.Contains("统一身份认证");
                
                Console.WriteLine($"检查结果 - 包含学生个人中心: {hasStudentCenter}, 包含登录: {hasLogin}");
                
                if (hasLogin && !hasStudentCenter)
                {
                    Console.WriteLine("页面显示需要登录");
                    return false;
                }
                
                if (hasStudentCenter)
                {
                    Console.WriteLine("✅ 登录状态有效");
                    return true;
                }
                
                // 如果没有明确的登录指示，输出页面内容供调试
                var preview = content.Length > 200 ? content.Substring(0, 200) : content;
                Console.WriteLine($"主页内容预览: {preview.Replace("\n", " ").Replace("\r", "")}...");
                
                // 作为最后的检查，我们假设如果页面正常加载且没有登录提示，就认为已登录
                return !hasLogin;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查登录状态时发生错误: {ex.Message}");
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
                Console.WriteLine("正在访问教务系统主页...");
                await _httpClient.GetAsync($"{_appSettings.BaseUrl}/jsxsd/");
                
                Console.WriteLine("正在发送登录请求...");
                var response = await _httpClient.PostAsync(loginUrl, formData);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"登录响应状态: {response.StatusCode}");
                
                // 检查登录状态
                Console.WriteLine("登录请求成功，正在验证登录状态...");
                // 等待一小段时间让会话生效
                await Task.Delay(1000);
                
                if (await CheckLoginStatusAsync())
                {
                    Console.WriteLine("✅ 登录成功！");
                    return true;
                }
                else
                {
                    Console.WriteLine("❌ 登录失败。");
                    
                    // 提供更详细的错误信息
                    if (responseContent.Contains("验证码"))
                        Console.WriteLine("登录失败，可能需要验证码。请检查教务系统登录页面。");
                    else if (responseContent.Contains("用户名或密码错误") || responseContent.Contains("密码不正确") || responseContent.Contains("用户名不存在"))
                        Console.WriteLine("登录失败，用户名或密码错误。");
                    else
                    {
                        Console.WriteLine("登录失败，未知错误。请检查网络或教务系统状态。");
                        // 输出部分响应内容用于调试
                        var preview = responseContent.Length > 200 ? responseContent.Substring(0, 200) : responseContent;
                        Console.WriteLine($"响应预览: {preview}...");
                    }
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

        public async Task<string?> GetEvaluationPageAsync()
        {
            Console.WriteLine("正在检查登录状态...");
            var loginStatus = await CheckLoginStatusAsync();
            Console.WriteLine($"登录状态检查结果: {loginStatus}");
            
            if (!loginStatus)
            {
                Console.WriteLine("❌ 未登录或会话已过期。");
                return null;
            }

            // 使用正确的评教页面URL - 基于诊断结果
            var evaluationUrl = $"{_appSettings.BaseUrl}/jsxsd/xspj/xspj_find.do";
            Console.WriteLine($"正在访问评教页面: {evaluationUrl}");
            
            try
            {
                var response = await _httpClient.GetAsync(evaluationUrl);
                Console.WriteLine($"评教页面响应状态: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ 访问评教页面失败: HTTP {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"评教页面内容长度: {content.Length} 字符");
                
                // 检查是否被重定向到登录页
                if (content.Contains("统一身份认证") || content.Contains("用户登录"))
                {
                    Console.WriteLine("❌ 会话可能已过期或重定向到登录页。");
                    return null;
                }
                
                // 检查评教系统是否开放
                if (content.Contains("评教") && content.Contains("未开放"))
                {
                    Console.WriteLine("⚠️  评教系统当前未开放。");
                    return null;
                }
                
                // 检查是否有评教内容
                if (content.Contains("评教") || content.Contains("进入评价") || content.Contains("学生评价"))
                {
                    Console.WriteLine("✅ 成功访问评教页面！");
                    return content;
                }
                else
                {
                    Console.WriteLine("⚠️  评教页面内容异常，可能评教系统未开放或页面结构已变更。");
                    // 输出部分内容用于调试
                    var preview = content.Length > 300 ? content.Substring(0, 300) : content;
                    Console.WriteLine($"页面内容预览: {preview.Replace("\n", " ").Replace("\r", "")}...");
                    return content; // 仍然返回内容，让后续解析器尝试处理
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取评教页面时发生错误: {ex.Message}");
                return null;
            }
        }

        public List<EvaluationLink> ParseEvaluationLinks(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                Console.WriteLine("HTML内容为空，无法解析。");
                return new List<EvaluationLink>();
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                
                var evaluationLinks = new List<EvaluationLink>();
                
                // 查找所有带有"进入评价"的链接
                var links = doc.DocumentNode.SelectNodes("//a[text()='进入评价']");
                
                if (links != null)
                {
                    foreach (var link in links)
                    {
                        var href = link.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href))
                        {
                            // 构建完整的URL
                            var fullUrl = href.StartsWith("/") ? $"{_appSettings.BaseUrl}{href}" : href;
                            
                            // 查找该链接所在行的其他信息
                            var row = link.Ancestors("tr").FirstOrDefault();
                            if (row != null)
                            {
                                var cells = row.SelectNodes(".//td");
                                if (cells?.Count >= 6)
                                {
                                    var evaluationInfo = new EvaluationLink
                                    {
                                        Url = fullUrl,
                                        Index = cells[0].InnerText.Trim(),
                                        Semester = cells[1].InnerText.Trim(),
                                        Category = cells[2].InnerText.Trim(),
                                        Batch = cells[3].InnerText.Trim(),
                                        StartTime = cells[4].InnerText.Trim(),
                                        EndTime = cells[5].InnerText.Trim()
                                    };
                                    evaluationLinks.Add(evaluationInfo);
                                }
                            }
                        }
                    }
                }
                
                return evaluationLinks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析评教链接时发生错误: {ex.Message}");
                return new List<EvaluationLink>();
            }
        }

        public void DisplayEvaluationInfo(List<EvaluationLink> evaluationLinks)
        {
            if (!evaluationLinks.Any())
            {
                Console.WriteLine("未找到评教链接。");
                return;
            }

            // Console.WriteLine("\n=== 评教信息 ===");
            for (int i = 0; i < evaluationLinks.Count; i++)
            {
                var info = evaluationLinks[i];
                Console.WriteLine($"\n{i + 1}. 评教项目:");
                Console.WriteLine($"   学年学期: {info.Semester}");
                Console.WriteLine($"   评价分类: {info.Category}");
                Console.WriteLine($"   评价批次: {info.Batch}");
                Console.WriteLine($"   开始时间: {info.StartTime}");
                Console.WriteLine($"   结束时间: {info.EndTime}");
                // Console.WriteLine($"   评教链接: {info.Url}");
            }
        }

        public async Task<string?> GetCourseListAsync(string evaluationUrl)
        {
            if (!await CheckLoginStatusAsync())
            {
                Console.WriteLine("用户未登录或会话已过期。");
                return null;
            }

            try
            {
                var response = await _httpClient.GetAsync(evaluationUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                if (content.Contains("统一身份认证") || content.Contains("用户登录"))
                {
                    Console.WriteLine("会话可能已过期或重定向到登录页。请尝试重新运行脚本。");
                    return null;
                }

                Console.WriteLine("成功访问评教课程列表页面！");
                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取评教课程列表页面时发生错误: {ex.Message}");
                return null;
            }
        }

        public List<CourseEvaluation> ParseCourseList(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                Console.WriteLine("HTML内容为空，无法解析。");
                return new List<CourseEvaluation>();
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                
                var courseTable = doc.DocumentNode.SelectSingleNode("//table[@id='dataList']");
                if (courseTable == null)
                {
                    Console.WriteLine("未找到课程数据表格。");
                    return new List<CourseEvaluation>();
                }

                var courses = new List<CourseEvaluation>();
                var rows = courseTable.SelectNodes(".//tr")?.Skip(1); // 跳过表头
                
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        var cells = row.SelectNodes(".//td");
                        if (cells?.Count >= 9)
                        {
                            var courseInfo = new CourseEvaluation
                            {
                                Index = cells[0].InnerText.Trim(),
                                CourseCode = cells[1].InnerText.Trim(),
                                CourseName = cells[2].InnerText.Trim(),
                                Teacher = cells[3].InnerText.Trim(),
                                EvaluationType = cells[4].InnerText.Trim(),
                                TotalScore = cells[5].InnerText.Trim(),
                                IsEvaluated = cells[6].InnerText.Trim(),
                                IsSubmitted = cells[7].InnerText.Trim()
                            };
                            
                            // 查找操作链接
                            var operationLinks = cells[8].SelectNodes(".//a");
                            string? evaluationLink = null;
                            
                            if (operationLinks != null)
                            {
                                foreach (var link in operationLinks)
                                {
                                    var href = link.GetAttributeValue("href", "");
                                    if (!string.IsNullOrEmpty(href) && href.Contains("xspj_edit.do"))
                                    {
                                        // 提取评教链接
                                        if (href.StartsWith("javascript:openWindow("))
                                        {
                                            // 提取括号内的URL部分
                                            var match = Regex.Match(href, @"'([^']*)'");
                                            if (match.Success)
                                            {
                                                evaluationLink = match.Groups[1].Value;
                                                if (evaluationLink.StartsWith("/"))
                                                    evaluationLink = $"{_appSettings.BaseUrl}{evaluationLink}";
                                            }
                                        }
                                    }
                                }
                            }
                            
                            courseInfo.EvaluationLink = evaluationLink;
                            courses.Add(courseInfo);
                        }
                    }
                }
                
                return courses;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析课程列表时发生错误: {ex.Message}");
                return new List<CourseEvaluation>();
            }
        }

        public List<CourseEvaluation> FindUnevaluatedCourses(List<CourseEvaluation> courses)
        {
            return courses.Where(course => course.IsSubmitted == "否").ToList();
        }

        public async Task<bool> PerformEvaluationAsync(CourseEvaluation courseInfo)
        {
            if (string.IsNullOrEmpty(courseInfo.EvaluationLink))
            {
                Console.WriteLine($"课程 {courseInfo.CourseName} 没有找到评教链接。");
                return false;
            }

            try
            {
                Console.WriteLine($"正在访问课程 {courseInfo.CourseName} 的评教页面...");
                var response = await _httpClient.GetAsync(courseInfo.EvaluationLink);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                if (content.Contains("统一身份认证") || content.Contains("用户登录"))
                {
                    Console.WriteLine("会话可能已过期或重定向到登录页。");
                    return false;
                }

                Console.WriteLine($"成功访问课程 {courseInfo.CourseName} 的评教页面！");
                
                var doc = new HtmlDocument();
                doc.LoadHtml(content);
                
                var form = doc.DocumentNode.SelectSingleNode("//form[@id='Form1']");
                if (form == null)
                {
                    Console.WriteLine("未找到评教表单。");
                    return false;
                }

                Console.WriteLine("找到评教表单，准备自动选择A选项并提交...");
                
                var formData = new Dictionary<string, string>();
                
                // 1. 收集所有隐藏字段
                var hiddenInputs = form.SelectNodes(".//input[@type='hidden']");
                if (hiddenInputs != null)
                {
                    foreach (var hiddenInput in hiddenInputs)
                    {
                        var name = hiddenInput.GetAttributeValue("name", "");
                        var value = hiddenInput.GetAttributeValue("value", "");
                        if (!string.IsNullOrEmpty(name))
                        {
                            formData[name] = value;
                        }
                    }
                }
                
                // 2. 处理主要评价指标 (pj0601id_ 字段) - 选择A选项
                var pjInputs = form.SelectNodes(".//input[contains(@name, 'pj0601id_')]");
                var pjGroups = new Dictionary<string, List<HtmlNode>>();
                
                if (pjInputs != null)
                {
                    foreach (var pjInput in pjInputs)
                    {
                        var name = pjInput.GetAttributeValue("name", "");
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!pjGroups.ContainsKey(name))
                                pjGroups[name] = new List<HtmlNode>();
                            pjGroups[name].Add(pjInput);
                        }
                    }
                    
                    // 为每个评价指标选择第一个选项（A选项）
                    foreach (var group in pjGroups)
                    {
                        if (group.Value.Any())
                        {
                            var firstOption = group.Value[0];
                            var value = firstOption.GetAttributeValue("value", "");
                            formData[group.Key] = value;
                            Console.WriteLine($"  - 评价指标 {group.Key}: 选择A选项");
                        }
                    }
                }
                
                // 3. 处理问卷调查 (tmid_ 字段) - 选择A选项
                var tmidInputs = form.SelectNodes(".//input[contains(@name, 'tmid_')]");
                var tmidGroups = new Dictionary<string, List<HtmlNode>>();
                
                if (tmidInputs != null)
                {
                    foreach (var tmidInput in tmidInputs)
                    {
                        var name = tmidInput.GetAttributeValue("name", "");
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!tmidGroups.ContainsKey(name))
                                tmidGroups[name] = new List<HtmlNode>();
                            tmidGroups[name].Add(tmidInput);
                        }
                    }
                    
                    // 为每个问卷题目选择第一个选项（A选项）
                    foreach (var group in tmidGroups)
                    {
                        if (group.Value.Any())
                        {
                            var firstOption = group.Value[0];
                            var value = firstOption.GetAttributeValue("value", "");
                            formData[group.Key] = value;
                            Console.WriteLine($"  - 问卷题目 {group.Key}: 选择A选项");
                        }
                    }
                }
                
                // 4. 设置提交状态
                formData["issubmit"] = "1";
                
                // 5. 其他意见建议（可选，留空）
                formData["jynr"] = "";
                
                // 提交表单
                var submitUrl = $"{_appSettings.BaseUrl}/xspj/xspj_save.do";
                
                Console.WriteLine("正在提交评教表单...");
                Console.WriteLine($"提交的数据项数量: {formData.Count}");
                
                var submitFormData = new FormUrlEncodedContent(formData);
                var submitResponse = await _httpClient.PostAsync(submitUrl, submitFormData);
                submitResponse.EnsureSuccessStatusCode();
                
                // 检查提交结果
                if (submitResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ 课程 {courseInfo.CourseName} 评教提交成功！");
                    
                    var submitContent = await submitResponse.Content.ReadAsStringAsync();
                    if (submitContent.Contains("成功") || submitContent.Contains("保存"))
                    {
                        Console.WriteLine("   评教数据已保存到系统");
                    }
                    else if (submitContent.Contains("错误") || submitContent.Contains("失败"))
                    {
                        Console.WriteLine("   ⚠️ 可能存在问题，请检查响应内容");
                    }
                    
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ 提交失败，HTTP状态码: {submitResponse.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理课程 {courseInfo.CourseName} 评教时发生错误: {ex.Message}");
                return false;
            }
        }

        public async Task AutoEvaluateCoursesAsync(string evaluationUrl)
        {
            Console.WriteLine("开始自动评教流程...");
            
            // 1. 获取课程列表
            var htmlContent = await GetCourseListAsync(evaluationUrl);
            if (string.IsNullOrEmpty(htmlContent))
            {
                Console.WriteLine("无法获取课程列表。");
                return;
            }
            
            // 2. 解析课程信息
            var courses = ParseCourseList(htmlContent);
            if (!courses.Any())
            {
                Console.WriteLine("未找到课程信息。");
                return;
            }
            
            Console.WriteLine($"找到 {courses.Count} 门课程。");
            
            // 3. 显示所有课程信息
            Console.WriteLine("\n=== 课程列表 ===");
            foreach (var course in courses)
            {
                Console.WriteLine($"课程: {course.CourseName} - 教师: {course.Teacher} - 已评: {course.IsEvaluated} - 已提交: {course.IsSubmitted}");
            }
            
            // 4. 查找未提交的评教
            var unevaluatedCourses = FindUnevaluatedCourses(courses);
            
            if (!unevaluatedCourses.Any())
            {
                Console.WriteLine("\n所有课程评教均已提交，无需进行评教操作。");
                return;
            }
            
            Console.WriteLine($"\n找到 {unevaluatedCourses.Count} 门课程需要进行评教：");
            foreach (var course in unevaluatedCourses)
            {
                Console.WriteLine($"- {course.CourseName} (教师: {course.Teacher})");
            }
            
            // 5. 对未提交的课程进行评教
            int successCount = 0;
            foreach (var course in unevaluatedCourses)
            {
                Console.WriteLine($"\n正在处理课程: {course.CourseName}");
                if (await PerformEvaluationAsync(course))
                {
                    successCount++;
                }
                else
                {
                    Console.WriteLine($"课程 {course.CourseName} 评教失败。");
                }
            }
            
            Console.WriteLine($"\n评教完成！成功处理 {successCount}/{unevaluatedCourses.Count} 门课程。");
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                Console.WriteLine("正在测试网络连接...");
                var response = await _httpClient.GetAsync($"{_appSettings.BaseUrl}/jsxsd/");
                Console.WriteLine($"主页访问状态: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"响应内容长度: {content.Length} 字符");
                    
                    if (content.Contains("统一身份认证") || content.Contains("登录") || content.Contains("教务系统"))
                    {
                        Console.WriteLine("✅ 成功访问教务系统主页");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("⚠️  主页内容异常，可能系统维护中");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"❌ 无法访问教务系统: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 网络连接测试失败: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DiagnoseEvaluationSystemAsync(string username, string password)
        {
            Console.WriteLine("🔍 开始评教系统诊断...\n");
            
            // 步骤1: 测试网络连接
            Console.WriteLine("📡 步骤1: 测试网络连接");
            if (!await TestConnectionAsync())
            {
                Console.WriteLine("❌ 网络连接失败，诊断结束");
                return false;
            }
            Console.WriteLine("✅ 网络连接正常\n");
            
            // 步骤2: 尝试登录
            Console.WriteLine("🔐 步骤2: 尝试登录");
            if (!await LoginAsync(username, password))
            {
                Console.WriteLine("❌ 登录失败，诊断结束");
                return false;
            }
            Console.WriteLine("✅ 登录成功\n");
            
            // 步骤3: 检查多个评教相关页面
            Console.WriteLine("🔍 步骤3: 检查评教相关页面");
            
            var evaluationUrls = new[]
            {
                "/xspj/xspj_find.do",
                "/xspj/xspj_list.do", 
                "/xspj/",
                "/jsxsd/xspj/xspj_find.do",
                "/jsxsd/xspj/xspj_list.do"
            };
            
            foreach (var path in evaluationUrls)
            {
                var fullUrl = $"{_appSettings.BaseUrl}{path}";
                Console.WriteLine($"正在尝试访问: {fullUrl}");
                
                try
                {
                    var response = await _httpClient.GetAsync(fullUrl);
                    Console.WriteLine($"  响应状态: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"  内容长度: {content.Length} 字符");
                        
                        // 检查关键词
                        var keywords = new[] { "评教", "进入评价", "学生评价", "教学评价", "课程评价" };
                        var foundKeywords = keywords.Where(k => content.Contains(k)).ToList();
                        
                        if (foundKeywords.Any())
                        {
                            Console.WriteLine($"  ✅ 找到评教相关内容: {string.Join(", ", foundKeywords)}");
                            
                            // 如果找到评教内容，输出更多信息
                            if (content.Contains("进入评价"))
                            {
                                var doc = new HtmlDocument();
                                doc.LoadHtml(content);
                                var links = doc.DocumentNode.SelectNodes("//a[text()='进入评价']");
                                Console.WriteLine($"  找到 {links?.Count ?? 0} 个'进入评价'链接");
                            }
                            
                            Console.WriteLine($"  🎯 这个页面看起来是有效的评教页面！");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine("  ⚠️  未找到评教相关内容");
                            
                            // 检查是否有错误信息
                            if (content.Contains("统一身份认证") || content.Contains("用户登录"))
                            {
                                Console.WriteLine("  ❌ 页面重定向到登录页面");
                            }
                            else if (content.Contains("错误") || content.Contains("异常"))
                            {
                                Console.WriteLine("  ❌ 页面包含错误信息");
                            }
                            else if (content.Contains("未开放") || content.Contains("暂停"))
                            {
                                Console.WriteLine("  ℹ️  评教系统可能未开放");
                            }
                            else
                            {
                                // 输出部分页面内容用于调试
                                var preview = content.Length > 200 ? content.Substring(0, 200) : content;
                                Console.WriteLine($"  页面内容预览: {preview.Replace("\n", " ").Replace("\r", "")}...");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ 访问失败: {ex.Message}");
                }
                
                Console.WriteLine();
            }
            
            Console.WriteLine("🔍 诊断完成");
            return false;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class EvaluationLink
    {
        public string Url { get; set; } = "";
        public string Index { get; set; } = "";
        public string Semester { get; set; } = "";
        public string Category { get; set; } = "";
        public string Batch { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
    }

    public class CourseEvaluation
    {
        public string Index { get; set; } = "";
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string Teacher { get; set; } = "";
        public string EvaluationType { get; set; } = "";
        public string TotalScore { get; set; } = "";
        public string IsEvaluated { get; set; } = "";
        public string IsSubmitted { get; set; } = "";
        public string? EvaluationLink { get; set; }
    }
}
