using JWSystem.Models;
using JWSystem.Services;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System.Text;

namespace JWSystem
{
    class Program
    {
        private static IConfiguration _configuration = null!;
        private static AppSettings _appSettings = null!;
        private static UserSecrets _userSecrets = null!;

        static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            _appSettings = _configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();
            _userSecrets = _configuration.GetSection("UserSecrets").Get<UserSecrets>() ?? new UserSecrets();

            AnsiConsole.MarkupLine("[bold yellow]=== 教务系统 C# 版本 ===[/]");

            if (string.IsNullOrEmpty(_userSecrets.Username) || string.IsNullOrEmpty(_userSecrets.Password))
            {
                AnsiConsole.MarkupLine("[yellow]提示：未在配置文件中找到用户名或密码。[/]");
                AnsiConsole.MarkupLine("[yellow]请手动输入账号信息 (本次运行有效):[/]");
                _userSecrets.Username = AnsiConsole.Ask<string>("[green]请输入学号:[/]")!;
                _userSecrets.Password = AnsiConsole.Prompt(
                    new TextPrompt<string>("[green]请输入密码:[/]")
                        .PromptStyle("red")
                        .Secret())!;
            }

            if (string.IsNullOrEmpty(_userSecrets.Username) || string.IsNullOrEmpty(_userSecrets.Password))
            {
                AnsiConsole.MarkupLine("[red]未提供账号密码，无法执行操作。按任意键退出...[/]");
                Console.ReadKey();
                return;
            }

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine($"[bold yellow]当前用户: {_userSecrets.Username}[/]");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[green]请选择要执行的功能：[/]")
                        .PageSize(10)
                        .AddChoices(new[]
                        {
                            "成绩查询 (cjcx)",
                            "课表查询 (jw)",
                            "考试安排查询 (kstx)",
                            "评教系统 (pj)",
                            "退出"
                        }));

                try
                {
                    switch (choice.Split(' ')[0])
                    {
                        case "成绩查询":
                            await RunGradeSystem();
                            break;
                        case "课表查询":
                            await RunScheduleSystem();
                            break;
                        case "考试安排查询":
                            await RunExamSystem();
                            break;
                        case "评教系统":
                            await RunEvaluationSystem();
                            break;
                        case "退出":
                            AnsiConsole.MarkupLine("[yellow]正在退出...[/]");
                            return;
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]程序执行出错: {ex.Message}[/]");
                    AnsiConsole.WriteException(ex);
                }

                if (choice != "退出")
                {
                    AnsiConsole.MarkupLine("[yellow]按任意键返回主菜单...[/]");
                    Console.ReadKey();
                }
            }
        }

        static async Task RunGradeSystem()
        {
            AnsiConsole.MarkupLine("\n[bold yellow]=== 成绩查询系统 ===[/]");
            
            using var gradeService = new GradeService(_appSettings);
            
            var previousGradesData = gradeService.LoadPreviousGrades();
            var previousFilteredGradesList = previousGradesData.RegularGrades;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("正在登录并获取成绩", async ctx =>
                {
                    if (await gradeService.LoginAsync(_userSecrets.Username, _userSecrets.Password))
                    {
                        AnsiConsole.MarkupLine("\n[green]登录成功，开始获取成绩信息...[/]");
                        var currentGradesFullData = await gradeService.GetGradesAsync();
                        
                        if (currentGradesFullData?.RegularGrades != null && currentGradesFullData.RegularGrades.Any())
                        {
                            AnsiConsole.MarkupLine("\n[green]成功获取常规成绩信息。[/]");

                            var now = DateTime.Now;
                            var currentYear = now.Year;
                            var academicYearStr = now.Month < 8 ? $"{currentYear - 1}-{currentYear}" : $"{currentYear}-{currentYear + 1}";
                            
                            AnsiConsole.MarkupLine($"[yellow]当前学年 (用于筛选): {academicYearStr}[/]");

                            var currentAcademicYearGrades = currentGradesFullData.RegularGrades
                                .Where(g => g.Semester.StartsWith(academicYearStr))
                                .ToList();

                            if (currentAcademicYearGrades.Any())
                            {
                                var table = new Table();
                                table.AddColumn("学期");
                                table.AddColumn("课程名称");
                                table.AddColumn("课程代码");
                                table.AddColumn("成绩");
                                table.AddColumn("学分");
                                table.AddColumn("绩点");

                                foreach (var g in currentAcademicYearGrades)
                                {
                                    table.AddRow(g.Semester, g.CourseName, g.CourseCode, g.Score, g.Credit, g.GPA);
                                }

                                AnsiConsole.Write(table);

                                var gradesToPushDict = new GradesData { RegularGrades = currentAcademicYearGrades };

                                if (gradeService.CompareGrades(currentGradesFullData.RegularGrades, previousFilteredGradesList))
                                {
                                    AnsiConsole.MarkupLine($"[yellow]检测到成绩变动或首次查询，准备推送 {academicYearStr} 学年常规成绩通知...[/]");
                                    await gradeService.PushGradesNotificationAsync(gradesToPushDict, _userSecrets.Username, _userSecrets.PushToken);
                                    gradeService.SaveGrades(currentGradesFullData);
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine($"[green]{academicYearStr} 学年常规成绩未发生变动，无需推送。[/]");
                                }
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[yellow]在 {academicYearStr} 学年未找到常规成绩记录。[/]");
                                if (gradeService.CompareGrades(new List<Grade>(), previousFilteredGradesList))
                                {
                                    AnsiConsole.MarkupLine("\n[yellow]检测到成绩变动（当前学年无成绩，但先前有记录），将清空已存成绩记录。[/]");
                                    gradeService.SaveGrades(new GradesData { RegularGrades = new List<Grade>() });
                                }
                                else if (!previousFilteredGradesList.Any())
                                {
                                    AnsiConsole.MarkupLine($"[green]先前也无 {academicYearStr} 学年成绩记录，无需操作。[/]");
                                }
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("\n[red]未能获取常规成绩信息或成绩为空。不进行比较或推送。[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("\n[red]登录失败，无法继续获取成绩。请检查账号密码及网络连接。[/]");
                    }
                });
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine();
        }

        static async Task RunScheduleSystem()
        {
            AnsiConsole.MarkupLine("\n[bold yellow]=== 课表查询系统 ===[/]");

            using var scheduleService = new ScheduleService(_appSettings);

            await AnsiConsole.Status()
                .StartAsync("正在登录并获取课表...", async ctx =>
                {
                    if (await scheduleService.LoginAsync(_userSecrets.Username, _userSecrets.Password))
                    {
                        var schedule = await scheduleService.GetScheduleAsync();
                        if (schedule != null)
                        {
                            await scheduleService.PushScheduleAsync(schedule, _userSecrets.PushToken);
                        }
                    }
                });
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine();
        }

        static async Task RunExamSystem()
        {
            AnsiConsole.MarkupLine("\n[bold yellow]=== 考试安排查询系统 ===[/]");

            using var examService = new ExamService(_appSettings);

            await AnsiConsole.Status()
                .StartAsync("正在登录并获取考试安排...", async ctx =>
                {
                    if (await examService.LoginAsync(_userSecrets.Username, _userSecrets.Password))
                    {
                        AnsiConsole.MarkupLine("\n[green]登录成功，开始访问考试查询页面...[/]");

                        var htmlContent = await examService.GetExamPageAsync();

                        if (!string.IsNullOrEmpty(htmlContent))
                        {
                            AnsiConsole.MarkupLine("\n[green]成功获取考试查询页面，正在解析可用学期...[/]");

                            var termOptions = examService.GetTermOptions(htmlContent);

                            if (termOptions.Any())
                            {
                                var selectedTerm = termOptions.FirstOrDefault(option => option.Selected);

                                if (selectedTerm != null)
                                {
                                    var termId = selectedTerm.Value;
                                    var termName = selectedTerm.Text;
                                    AnsiConsole.MarkupLine($"[yellow]默认选中学期: {termName} (ID: {termId})[/]");

                                    var examListHtml = await examService.GetExamListAsync(termId);

                                    if (!string.IsNullOrEmpty(examListHtml))
                                    {
                                        AnsiConsole.MarkupLine("\n[green]成功获取考试安排，正在解析...[/]");

                                        var exams = examService.ParseExamList(examListHtml);

                                        if (exams.Any())
                                        {
                                            AnsiConsole.MarkupLine($"[green]找到 {exams.Count} 门考试安排:[/]");

                                            var sortedExams = examService.SortExamsByDate(exams);

                                            var table = new Table();
                                            table.Expand();
                                            table.AddColumn("课程名称");
                                            table.AddColumn("课程代码");
                                            table.AddColumn("考试时间");
                                            table.AddColumn("考场地点");
                                            table.AddColumn("座位号");
                                            table.AddColumn("考试方式");
                                            table.AddColumn("备注");
                                            table.AddColumn("状态");

                                            foreach (var exam in sortedExams)
                                            {
                                                var examTime = examService.FormatExamTime(exam.ExamTime);
                                                var daysUntil = examService.CountDaysUntilExam(examTime.Date);
                                                var daysText = daysUntil == null ? "[grey]未知[/]" :
                                                               daysUntil == 0 ? "[bold red]今天[/]" :
                                                               daysUntil < 0 ? "[grey]已结束[/]" : $"[bold green]还有 {daysUntil} 天[/]";

                                                table.AddRow(
                                                    exam.CourseName,
                                                    exam.CourseCode,
                                                    examTime.Full,
                                                    exam.ExamRoom,
                                                    exam.SeatNumber ?? "",
                                                    exam.ExamMethod ?? "",
                                                    exam.Remarks ?? "",
                                                    daysText
                                                );
                                            }

                                            AnsiConsole.Write(table);

                                            AnsiConsole.MarkupLine("\n[yellow]正在检查是否有近期考试...[/]");
                                            var upcomingExams = examService.GetUpcomingExams(sortedExams);

                                            if (upcomingExams.Any())
                                            {
                                                AnsiConsole.MarkupLine($"[yellow]找到 {upcomingExams.Count} 门近期考试，准备推送微信提醒...[/]");
                                                if (await examService.PushExamsAsync(exams, termName, _userSecrets.PushToken))
                                                {
                                                    AnsiConsole.MarkupLine("[green]考试安排已成功推送！[/]");
                                                }
                                                else
                                                {
                                                    AnsiConsole.MarkupLine("[red]考试安排推送失败。[/]");
                                                }
                                            }
                                            else
                                            {
                                                AnsiConsole.MarkupLine("\n[green]没有近期考试（一周内），无需推送微信提醒。[/]");
                                            }
                                        }
                                        else
                                        {
                                            AnsiConsole.MarkupLine("[yellow]未找到考试安排。[/]");
                                        }
                                    }
                                    else
                                    {
                                        AnsiConsole.MarkupLine("[red]获取考试安排失败。[/]");
                                    }
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine("[red]未找到默认选中的学期。[/]");
                                }
                            }
                            else
                            {
                                AnsiConsole.MarkupLine("[red]未找到学期选项。[/]");
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[red]获取考试查询页面失败。[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]登录失败，无法获取考试安排。[/]");
                    }
                });
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine();
        }

        static async Task RunEvaluationSystem()
        {
            AnsiConsole.MarkupLine("\n[bold yellow]=== 评教系统 ===[/]");

            // 检查当前日期是否在评教期间
            var now = DateTime.Now;
            if (now.Month >= 1 && now.Month <= 3) // 春季学期评教通常在1-3月
            {
                AnsiConsole.MarkupLine("[yellow]当前可能处于春季学期评教期间[/]");
            }
            else if (now.Month >= 6 && now.Month <= 8) // 秋季学期评教通常在6-8月
            {
                AnsiConsole.MarkupLine("[yellow]当前可能处于秋季学期评教期间[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[orange3]⚠️  注意：当前时间可能不在评教开放期间，评教系统可能无法访问[/]");
            }

            using var evaluationService = new EvaluationService(_appSettings);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("正在登录并处理评教...", async ctx =>
                {
                    ctx.Status("正在连接教务系统...");
                    
                    if (await evaluationService.LoginAsync(_userSecrets.Username, _userSecrets.Password))
                    {
                        AnsiConsole.MarkupLine("\n[green]✅ 登录成功，开始访问评教页面...[/]");
                        ctx.Status("正在获取评教页面...");

                        var htmlContent = await evaluationService.GetEvaluationPageAsync();

                        if (!string.IsNullOrEmpty(htmlContent))
                        {
                            AnsiConsole.MarkupLine("\n[green]✅ 成功获取评教页面内容。[/]");
                            ctx.Status("正在解析评教链接...");

                            var evaluationLinks = evaluationService.ParseEvaluationLinks(htmlContent);
                            evaluationService.DisplayEvaluationInfo(evaluationLinks);

                            if (evaluationLinks.Any())
                            {
                                ctx.Status("正在执行自动评教...");
                                var targetLink = evaluationLinks.First();
                                AnsiConsole.Write(new Rule("[yellow]开始自动评教流程[/]").Centered());
                                await evaluationService.AutoEvaluateCoursesAsync(targetLink.Url);
                                AnsiConsole.Write(new Rule("[green]自动评教流程结束[/]").Centered());
                            }
                            else
                            {
                                AnsiConsole.MarkupLine("\n[yellow]⚠️  未找到评教链接。可能原因：[/]");
                                AnsiConsole.MarkupLine("[yellow]   • 当前不在评教开放时间[/]");
                                AnsiConsole.MarkupLine("[yellow]   • 所有课程已完成评教[/]");
                                AnsiConsole.MarkupLine("[yellow]   • 评教系统暂时关闭[/]");
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("\n[red]❌ 未能获取评教页面内容。[/]");
                            AnsiConsole.MarkupLine("[yellow]可能原因：评教系统未开放或页面结构已变更[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("\n[red]❌ 登录失败，无法继续访问评教页面。[/]");
                        AnsiConsole.MarkupLine("[yellow]💡 建议检查：[/]");
                        AnsiConsole.MarkupLine("[yellow]   • 账号密码是否正确[/]");
                        AnsiConsole.MarkupLine("[yellow]   • 网络连接是否正常[/]");
                        AnsiConsole.MarkupLine("[yellow]   • 教务系统是否需要验证码[/]");
                        AnsiConsole.MarkupLine("[yellow]   • 是否在评教开放时间内[/]");
                    }
                });
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine();
        }
    }
}
