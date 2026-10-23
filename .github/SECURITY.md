# Security Policy / 安全政策

## Supported Versions / 支持范围

安全修复针对最新正式版。旧版不保证获得回移修复；报告时请注明具体版本或提交。此政策不代表已经完成安全审计。

Security fixes target the latest stable release. Backports to older releases are not guaranteed. Include the affected version or commit; this policy does not imply a completed security audit.

## Private Reporting / 私密报告

请使用本仓库的 [Report a vulnerability](https://github.com/EnchantGapple233/Minedock/security/advisories/new) 入口提交私密报告，不要先创建公开 Issue、Discussion 或包含利用细节的 Pull Request。需要 GitHub 账号。

Use this repository's private **Report a vulnerability** form. Do not initially publish an issue, discussion, or pull request containing exploitation details. A GitHub account is required.

报告中请提供 / Please include:

- 受影响版本、Windows 环境及必要的加载器 / 启动器版本。Affected version, Windows environment, and relevant loader / launcher versions.
- 可在隔离测试目录复现的最小步骤与脱敏日志。Minimal steps in an isolated test directory and redacted logs.
- 可能影响、利用前提及修复建议（如有）。Potential impact, prerequisites, and suggested fixes if available.

重点关注更新来源与校验绕过、路径穿越、非预期文件覆盖、命令执行、权限与隐私泄露。普通安装失败或上游网络不可用请使用 Issue / Discussions。

Relevant areas include update provenance and hash bypasses, path traversal, unintended file replacement, command execution, permissions, and privacy. Use issues or discussions for ordinary installation errors or unavailable upstream services.

不要上传真实账号、访问令牌、个人存档或第三方私密数据，不要对他人目录或服务进行破坏性测试。

Do not include real accounts, access tokens, personal saves, or third-party private data. Avoid destructive tests against other people's directories or services.

维护者会在有能力处理时跟进并协商披露；个人项目不承诺固定响应或修复期限，也没有漏洞奖金计划。内置第三方启动器的独立漏洞请同步向相应上游私密报告；MineDock 的集成和分发问题仍可在此报告。

The maintainer will follow up and coordinate disclosure as capacity allows. This personal project promises no fixed response or fix deadline and has no bug bounty program. Privately report standalone bundled-launcher vulnerabilities upstream too; MineDock integration or distribution issues remain in scope here.
