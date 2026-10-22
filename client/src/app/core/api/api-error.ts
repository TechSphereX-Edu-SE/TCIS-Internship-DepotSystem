import { HttpErrorResponse } from '@angular/common/http';

// Shape of the RFC 7807 problem details returned by the API (ResultExtensions.ToProblemDetails / ValidationFilter)
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

// What a page needs to show an API error: one message plus optional per-field messages
export interface ApiError {
  status: number;
  code: string | null;
  message: string;
  // Raw English detail from the API, useful as a hint under the Vietnamese message
  detail: string | null;
  fieldErrors: Record<string, string[]>;
}

// Vietnamese text for every domain error code the API can return (Domain + Application layers)
const MESSAGES: Record<string, string> = {
  'Auth.InvalidCredentials': 'Email hoặc mật khẩu không đúng.',
  'Auth.InvalidRefreshToken': 'Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.',
  'Auth.EmailTaken': 'Email đã được sử dụng.',
  'Auth.RegistrationFailed': 'Không tạo được tài khoản.',
  'Block.BayOutOfRange': 'Bay nằm ngoài kích thước của block.',
  'Block.BayParity': '20ft xếp bay lẻ, 40ft xếp bay chẵn.',
  'Block.ContainersOutsideNewSize': 'Còn container nằm ngoài kích thước mới, chuyển container trước.',
  'Block.Duplicate': 'Mã block đã tồn tại.',
  'Block.InvalidSize': 'Kích thước block không hợp lệ (bay, row ≥ 1 và tier ≥ 2).',
  'Block.NotFound': 'Không tìm thấy block.',
  'Block.SlotOutOfRange': 'Vị trí nằm ngoài kích thước của block.',
  'Block.SlotRequired': 'Block thật cần nhập đủ Bay, Row, Tier.',
  'Block.VirtualCannotResize': 'Block ảo không có bay/row/tier.',
  'Block.VirtualHasNoSlot': 'Block ảo không nhập Bay, Row, Tier.',
  'Container.Duplicate': 'Số container đã tồn tại.',
  'Container.HasHistory': 'Container đã có lịch sử ra/vào bãi nên không xoá được.',
  'Container.NotFound': 'Không tìm thấy container.',
  'Container.SizeChangeInYard': 'Container đang ở trong bãi nên không đổi được kích thước.',
  'ContainerVisit.AlreadyGatedOut': 'Container đã xuất bãi.',
  'ContainerVisit.AlreadyInYard': 'Container đang ở trong bãi.',
  'ContainerVisit.ContainerAbove': 'Có container khác đang xếp phía trên.',
  'ContainerVisit.ContainerDamaged': 'Container đang hư hỏng, cập nhật tình trạng trước khi xuất.',
  'ContainerVisit.NoSupportBelow': 'Không có container đỡ bên dưới.',
  'ContainerVisit.NotInYard': 'Container không ở trong bãi.',
  'ContainerVisit.SamePosition': 'Vị trí mới trùng vị trí hiện tại.',
  'ContainerVisit.SlotOccupied': 'Vị trí đã có container.',
  'ContainerVisit.WrongLineOperator': 'Lệnh giao không cùng hãng với container.',
  'Customer.Duplicate': 'Mã số thuế đã tồn tại.',
  'Customer.NotFound': 'Không tìm thấy khách hàng.',
  'DeliveryOrder.AlreadyExpired': 'Hạn lệnh phải từ hôm nay trở đi.',
  'DeliveryOrder.Duplicate': 'Số lệnh đã tồn tại.',
  'DeliveryOrder.DuplicateLine': 'Mỗi loại container chỉ một dòng.',
  'DeliveryOrder.Expired': 'Lệnh giao đã hết hạn.',
  'DeliveryOrder.InvalidQuantity': 'Số lượng phải từ 1 trở lên.',
  'DeliveryOrder.NotFound': 'Không tìm thấy lệnh giao.',
  'DeliveryOrder.QuantityExceeded': 'Lệnh đã giao đủ số lượng cho loại container này.',
  'DeliveryOrder.TypeNotAllowed': 'Lệnh không có loại container này.',
  'LineOperator.Duplicate': 'Mã hãng đã tồn tại.',
  'LineOperator.NotFound': 'Không tìm thấy hãng khai thác.',
};

const FALLBACK = 'Có lỗi xảy ra, vui lòng thử lại.';

// Converts any error from HttpClient into one consistent object the UI can display
export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: -1, code: null, message: FALLBACK, detail: null, fieldErrors: {} };
  }

  // Status 0 means the request never reached the API (Aspire stopped, proxy not running, CORS)
  if (error.status === 0) {
    return { status: 0, code: null, message: 'Không kết nối được API. Kiểm tra Aspire đã chạy chưa.', detail: null, fieldErrors: {} };
  }

  const body = (typeof error.error === 'object' && error.error !== null ? error.error : {}) as ProblemDetails;
  const code = body.errorCode ?? null;
  const fieldErrors = body.errors ?? {};
  const hasFieldErrors = Object.keys(fieldErrors).length > 0;

  let message = (code && MESSAGES[code]) || body.detail || body.title || FALLBACK;
  if (hasFieldErrors && !code) {
    message = 'Dữ liệu chưa hợp lệ, kiểm tra các ô được đánh dấu.';
  }
  if (error.status === 401) {
    message = 'Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.';
  }

  return { status: error.status, code, message, detail: body.detail ?? null, fieldErrors };
}

// First validation message for one request field; the API uses PascalCase names (e.g. "ContainerNumber")
export function fieldError(apiError: ApiError | null, field: string): string | null {
  if (!apiError) return null;
  const key = Object.keys(apiError.fieldErrors).find((k) => k.toLowerCase() === field.toLowerCase());
  return key ? apiError.fieldErrors[key][0] ?? null : null;
}
