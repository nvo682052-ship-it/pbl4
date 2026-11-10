# DEMO 5 PHUT - pbl4

## 1. Chay ung dung

Mo solution:

```text
C:\Users\ADMIN\Documents\Codex\2026-09-14\role-b-n-l-m-t\pbl4\pbl4.sln
```

Trong Visual Studio:

1. Chuot phai `pbl4.UI`.
2. Chon **Set as Startup Project**.
3. Bam **Start**.

Khong can chay API, khong can host web.

## 2. Kich ban demo

### Phut 0:00 - 1:00

Gioi thieu de tai:

> De tai cua em nghien cuu Windows Thread Pool va Overlapped I/O de toi uu trinh tai xuong HTTP phan doan. Ung dung duoc lam bang WinForms, tai truc tiep tren may nguoi dung, khong phai web public.

### Phut 1:00 - 2:00

Nhap URL file, output path, chon `Threads = 4` hoac `8`, bam `Tai`. Cot `Chunks` co the lon hon so Threads; vi du 4 worker co toi da 16 chunk nho.

Noi:

> Engine gui HTTP HEAD de kiem tra `Accept-Ranges: bytes`. Neu server ho tro, file duoc chia thanh nhieu segment va moi segment gui `Range: bytes=start-end`.

### Phut 2:00 - 3:00

Bam Pause/Resume.

Noi:

> Progress tung segment duoc luu vao SQLite. Khi resume, engine tinh `NextOffset = Start + DownloadedBytes` va chi tai tiep byte con thieu.

### Phut 3:00 - 4:00

Mo `DownloadEngine.cs` va `ThreadPoolManager.cs`.

Noi:

> Mỗi worker lấy chunk từ hàng đợi. Tải xong một chunk thì worker đó lấy chunk còn lại tiếp theo, nên worker nhanh có thể xử lý thêm việc đang chờ. Số worker chạy đồng thời vẫn bị giới hạn theo lựa chọn Threads. File được ghi bằng `RandomAccess.WriteAsync` với file handle async, đây là Overlapped I/O trên Windows.

### Phut 4:00 - 5:00

Chay benchmark:

```powershell
.\scripts\benchmark.ps1 -Url "http://speedtest.tele2.net/100MB.zip"
```

Noi:

> Benchmark so sanh 1, 4 va 8 worker. Chia chunk nhỏ hơn giúp cân bằng tải khi tốc độ từng request khác nhau; kết quả vẫn tùy server và mạng, vì quá nhiều request có thể tăng overhead hoặc bị giới hạn.

## 3. Cau hoi va tra loi mau

### Vi sao khong lam web public?

Neu lam web public, server phai ganh bang thong, socket, CPU va disk I/O cho nhieu nguoi. De tai nay tap trung Windows Thread Pool va Overlapped I/O tren may nguoi dung, nen desktop app hop ly hon.

### ThreadPool nam o dau?

Trong `ThreadPoolManager.cs` va `DownloadEngine.cs`. Segment worker duoc chay qua `Task.Run`, khong tao thread thu cong.

### Overlapped I/O nam o dau?

Trong `SafeFileWriter.cs`. File duoc mo voi `FileOptions.Asynchronous | FileOptions.RandomAccess`, sau do ghi theo offset bang `RandomAccess.WriteAsync`.

### HTTP Range la gi?

La co che HTTP cho phep tai mot khoang byte cua file, vi du `Range: bytes=0-1023`. Server hop le tra `206 Partial Content`.

### Deadlock duoc tranh nhu the nao?

Khong giu lock khi network I/O, gioi han critical section, dung timeout voi semaphore/lock va cancellation token cho pause/cancel.
