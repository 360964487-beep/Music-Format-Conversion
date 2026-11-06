# 线上网页版部署

Docker 镜像同时提供轻转页面和视频下载服务。访问容器对应的 HTTPS 网址后，点击「视频下载」、粘贴分享链接并提交。视频准备好后，页面会触发浏览器保存。

- 在 Docker 主机上从项目根目录构建镜像，运行时设置 `QZ_PUBLIC_ORIGIN` 为实际 HTTPS 网址的 origin，容器默认监听 10000 端口。用反向代理终止 HTTPS，并保留 Host 请求头。平台提供 PORT 时会使用该端口。
- 在 Render 创建 Docker Web Service 时，选择仓库根目录的 Dockerfile，健康检查路径设置为 `/healthz`。默认使用 Render 注入的 `RENDER_EXTERNAL_URL`。自定义域名时设置 `QZ_PUBLIC_ORIGIN`。
- 首次部署后用有权下载的视频验证 B 站和抖音。线上服务不接收浏览器 Cookie，平台要求验证的视频会给出提示。
