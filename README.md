# Kid Guard Backend

ASP.NET Core 8 API của hệ thống Kid Guard, phục vụ ứng dụng Flutter cho ba vai trò Admin, Parent và Child.

## Chức năng chính

- Đăng ký bằng email và xác thực Email OTP; đăng nhập JWT với SMS OTP.
- Quản lý tài khoản Admin, Parent và Child.
- Ghép thiết bị Child bằng QR.
- Đồng bộ GPS, pin, âm thanh và chuyển động.
- Gửi và xử lý cảnh báo SOS.
- Quản lý quan hệ Parent → Child → thiết bị.
- Swagger tại `/swagger` và kiểm tra sức khỏe tại `/health`.

## Chạy cục bộ

Sao chép `appsettings.Example.json` thành `appsettings.json`, điền SQL Server và JWT secret, sau đó chạy:

```powershell
dotnet restore
dotnet run
```

API mặc định chạy tại `http://localhost:5111`.

## Cấu hình triển khai

Có thể cấu hình bằng biến môi trường:

- `PORT`
- `ConnectionStrings__DefaultConnection`
- `JWTKey__Secret`
- `JWTKey__ValidIssuer`
- `JWTKey__ValidAudience`
- `SmsGateway__Url`, `SmsGateway__Username`, `SmsGateway__Password`
- `KIDGUARD_EMAIL_HOST`, `KIDGUARD_EMAIL_PORT`
- `KIDGUARD_EMAIL_USERNAME`, `KIDGUARD_EMAIL_PASSWORD`
- `KIDGUARD_EMAIL_FROM`, `KIDGUARD_EMAIL_FROM_NAME`
- `KIDGUARD_EMAIL_ENABLE_SSL`

`appsettings.json` được loại khỏi Git để không công khai thông tin nhạy cảm.

