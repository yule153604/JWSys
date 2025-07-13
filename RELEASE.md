# 🎉 JWSystem v1.0.0 - 首个正式版本

**教务系统自动化工具** - 基于 .NET 8.0 的现代化教务系统自动化解决方案

## ✨ 核心功能

| 功能模块 | 描述 | 状态 |
|---------|------|------|
| 🎯 **成绩查询** | 自动监控成绩变动，微信推送通知 | ✅ 完整支持 |
| 📅 **课表查询** | 智能课表查询，HTML美化展示 | ✅ 完整支持 |
| 📝 **考试安排** | 考试时间查询，近期考试提醒 | ✅ 完整支持 |
| ⭐ **自动评教** | 一键批量评教，智能选项选择 | ✅ 完整支持 |

## 🚀 快速开始

### 方式一：安装包（推荐）
1. 下载 `JWSystem-Setup-v1.0.exe` **(2.64MB)**
2. 运行安装程序完成部署
3. 配置账号信息即可使用

### 方式二：源码运行
```bash
# 克隆仓库
git clone https://github.com/yule153604/JWSys.git
cd JWSys

# 配置账号信息
cd JWSystem
# 编辑 appsettings.Development.json 填入你的账号信息

# 运行程序
cd ..
run.bat
```

## ⚙️ 配置说明

### 第一次使用需要配置
编辑 `JWSystem/appsettings.Development.json` 配置文件填入你的信息：

```json
{
  "UserSecrets": {
    "Username": "你的学号",
    "Password": "你的密码",
    "PushToken": "PushPlus Token（可选）"
  }
}
```

### PushPlus微信推送（可选）
1. 访问 [PushPlus官网](http://www.pushplus.plus/) 注册
2. 获取Token填入配置文件
3. 关注微信公众号接收推送

## 🔧 技术特性

- **🏗️ 现代架构**: .NET 8.0 + C# 12 + Spectre.Console
- **🔒 安全设计**: 配置分离 + 隐私保护 + 本地存储
- **📦 易于部署**: 专业安装包 + 一键运行脚本
- **🌐 网络优化**: 异步HTTP + 智能重试机制

## 📋 系统要求

- **操作系统**: Windows 10/11 (x64)
- **运行时**: .NET 8.0 Runtime 或更高版本
- **网络**: 校园网环境，能访问 `jw.cupk.edu.cn`
- **磁盘**: 50MB 可用空间

## 🔒 安全说明

- ✅ **隐私保护**: 敏感配置不会上传到GitHub
- ✅ **本地存储**: 所有数据仅保存在本地
- ✅ **开源透明**: 完全开源，可审查代码
- ✅ **加密通信**: HTTPS与教务系统安全通信

## 📦 安装包内容

- `JWSystem.exe` - 主程序
- `run.bat` - 启动脚本
- `appsettings.json` - 系统配置
- `appsettings.Development.json` - 用户配置文件
- 所有必需的.NET运行时依赖

## 🐛 已知限制

- ⚠️ 需要校园网环境
- ⚠️ 验证码需要手动处理
- ⚠️ 部分学期信息可能需要调整

## 💬 获取帮助

- 🐛 **问题反馈**: [GitHub Issues](https://github.com/yule153604/JWSys/issues)
- 💡 **功能建议**: [GitHub Discussions](https://github.com/yule153604/JWSys/discussions)
- 📖 **详细文档**: [README.md](https://github.com/yule153604/JWSys#readme)

## 🤝 贡献代码

欢迎提交 Issues 和 Pull Requests！

1. Fork 仓库
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交修改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

## 📄 开源协议

本项目基于 [MIT License](https://github.com/yule153604/JWSys/blob/main/LICENSE) 开源。

---

**🎓 让教务管理更简单！** 

如果这个项目对你有帮助，请给个 ⭐ **Star** 支持一下！
