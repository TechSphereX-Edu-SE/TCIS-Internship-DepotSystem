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
  'Block.Archived': 'Block đã lưu trữ, chỉ còn để tra cứu lịch sử.',
  'Block.BayOutOfRange': 'Bay nằm ngoài kích thước của block.',
  'Block.BayParity': '20ft xếp bay lẻ, 40ft xếp bay chẵn.',
  'Block.CategoryNotAllowed': 'Block này không nhận loại container này.',
  'Block.ContainersOfRemovedType': 'Block còn container thuộc loại vừa bỏ, chuyển container đó trước.',
  'Block.ContainersOutsideNewSize': 'Còn container nằm ngoài kích thước mới, chuyển container trước.',
  'Block.Duplicate': 'Mã block đã tồn tại.',
  'Block.HasContainers': 'Block còn container, chuyển hết container ra trước khi xoá.',
  'Block.InvalidSize': 'Kích thước block không hợp lệ (bay, row ≥ 1 và tier ≥ 2).',
  'Block.NoCategories': 'Chọn ít nhất một loại container cho block.',
  'Block.NotFound': 'Không tìm thấy block.',
  'Block.SlotOutOfRange': 'Vị trí nằm ngoài kích thước của block.',
  'Block.SlotRequired': 'Block thật cần nhập đủ Bay, Row, Tier.',
  'Block.VirtualCannotResize': 'Block ảo không có bay/row/tier.',
  'Block.VirtualHasNoSlot': 'Block ảo không nhập Bay, Row, Tier.',
  'Container.Duplicate': 'Số container đã tồn tại.',
  'Container.HasHistory': 'Container đã có lịch sử ra/vào bãi hoặc quan hệ khai thác nên không xoá được.',
  'Container.NotFound': 'Không tìm thấy container.',
  'Container.RepairCaseOpen': 'Container đang có hồ sơ giám định/sửa chữa chưa đạt. Hoàn tất hồ sơ ở màn Giám định & sửa chữa.',
  'Container.SizeChangeInYard': 'Container đang ở trong bãi nên không đổi được kích thước.',
  'ContainerVisit.AlreadyGatedOut': 'Container đã xuất bãi.',
  'ContainerVisit.AlreadyInYard': 'Container đang ở trong bãi.',
  'ContainerVisit.ContainerAbove': 'Có container khác đang xếp phía trên.',
  'ContainerVisit.NotFound': 'Không tìm thấy lượt ra/vào này.',
  'ContainerVisit.ContainerDamaged': 'Container đang hư hỏng, chưa xuất bãi được.',
  'ContainerVisit.NoOperatingAssignment':
    'Container chưa có quan hệ khai thác đã xác nhận cho ngày nhập, nên chưa biết hãng khai thác. Ghi nhận ở Danh mục · Container.',
  'ContainerVisit.NoSupportBelow': 'Không có container đỡ bên dưới.',
  'ContainerVisit.NotInYard': 'Container không ở trong bãi.',
  'ContainerVisit.RepairCaseOpen': 'Container có hồ sơ giám định/sửa chữa chưa đạt, chưa xuất bãi được.',
  'ContainerVisit.PositionChanged': 'Container vừa được chuyển chỗ khác. Đã tải lại vị trí mới, kiểm tra rồi thử lại.',
  'ContainerVisit.OperatorMismatch': 'Hãng khai thác khác với quan hệ khai thác đã xác nhận của container.',
  'ContainerVisit.SamePosition': 'Vị trí mới trùng vị trí hiện tại.',
  'ContainerVisit.SlotOccupied': 'Vị trí đã có container.',
  'ContainerVisit.WrongLineOperator': 'Lệnh giao không cùng hãng với container.',
  'Customer.Duplicate': 'Mã số thuế đã tồn tại.',
  'Customer.NotFound': 'Không tìm thấy khách hàng.',
  'Data.ConcurrentUpdate': 'Dữ liệu vừa được thao tác khác thay đổi cùng lúc. Tải lại rồi thử lại.',
  'DeliveryOrder.AlreadyCancelled': 'Lệnh giao đã huỷ trước đó.',
  'DeliveryOrder.AlreadyExpired': 'Hạn lệnh phải từ hôm nay trở đi.',
  'DeliveryOrder.Cancelled': 'Lệnh giao đã huỷ, không xuất bãi theo lệnh này được.',
  'DeliveryOrder.Duplicate': 'Số lệnh đã tồn tại.',
  'DeliveryOrder.DuplicateLine': 'Mỗi loại container và kích thước chỉ một dòng.',
  'DeliveryOrder.Expired': 'Lệnh giao đã hết hạn.',
  'DeliveryOrder.HasReleases': 'Lệnh đã có container xuất theo lệnh nên không huỷ được.',
  'DeliveryOrder.InvalidQuantity': 'Số lượng phải từ 1 trở lên.',
  'DeliveryOrder.NotFound': 'Không tìm thấy lệnh giao.',
  'DeliveryOrder.QuantityExceeded': 'Lệnh đã giao đủ số lượng cho loại container này.',
  'DeliveryOrder.TypeNotAllowed': 'Lệnh không có loại và kích thước container này.',
  'ExternalRepair.Active': 'Container đang chuyển sửa ngoài. Ghi nhận container về bãi (hoặc huỷ yêu cầu) trước khi nghiệm thu đạt.',
  'ExternalRepair.AlreadyActive': 'Hồ sơ đã có yêu cầu sửa ngoài chưa hoàn tất.',
  'ExternalRepair.CaseNotApproved': 'Chỉ chuyển sửa ngoài khi hồ sơ đã duyệt sửa hoặc đang sửa.',
  'ExternalRepair.NotFound': 'Không tìm thấy yêu cầu sửa ngoài.',
  'ExternalRepair.NotInYard': 'Container không ở trong bãi nên không chuyển sửa ngoài được.',
  'ExternalRepair.NotOut': 'Container chưa xuất đi sửa ngoài.',
  'ExternalRepair.NotRequested': 'Yêu cầu sửa ngoài không còn chờ xuất (đã huỷ hoặc đã xuất).',
  'LineOperator.Duplicate': 'Mã đối tác đã tồn tại.',
  'LineOperator.NotFound': 'Không tìm thấy đối tác.',
  'OperatingAssignment.AlreadyCancelled': 'Quan hệ khai thác đã huỷ trước đó.',
  'OperatingAssignment.InUse': 'Đã có lượt vào bãi dựa trên quan hệ này, chỉ kết thúc được, không huỷ được.',
  'OperatingAssignment.InvalidPeriod': 'Ngày kết thúc không được trước ngày bắt đầu.',
  'OperatingAssignment.NoneActive': 'Không có quan hệ khai thác đã xác nhận cho ngày này.',
  'OperatingAssignment.NotConfirmed': 'Chỉ kết thúc được quan hệ đã xác nhận.',
  'OperatingAssignment.NotFound': 'Không tìm thấy quan hệ khai thác.',
  'OperatingAssignment.NotPending': 'Chỉ xác nhận được quan hệ đang chờ xác nhận.',
  'OperatingAssignment.Overlap': 'Trùng giai đoạn với một quan hệ khai thác đã xác nhận khác. Kết thúc quan hệ đó trước.',
  'OperatingAssignment.UsedAfterEnd': 'Đã có lượt vào bãi sau ngày kết thúc này, chọn ngày kết thúc muộn hơn.',
  'RepairCase.Closed': 'Hồ sơ đã đạt và đóng, không thay đổi được nữa.',
  'RepairCase.InvalidTransition': 'Bước này không còn hợp lệ với trạng thái hiện tại của hồ sơ. Tải lại hồ sơ.',
  'RepairCase.NotFound': 'Không tìm thấy hồ sơ sửa chữa.',
  'RepairCase.ReasonRequired': 'Ghi lý do khi nghiệm thu không đạt.',
  'RepairCase.EstimateRequired': 'Việc sửa được duyệt bằng cách duyệt một phiên bản dự toán đã gửi.',
  'RepairEstimate.ApproverRequired': 'Ghi bên đã duyệt dự toán (VD: hãng khai thác).',
  'RepairEstimate.DisputeReasonRequired': 'Ghi lý do tranh chấp chi phí.',
  'RepairEstimate.LineIncomplete': 'Mỗi hạng mục cần mô tả hư hỏng và phương án sửa.',
  'RepairEstimate.LineNotFound': 'Hạng mục không còn trong phiên bản dự toán này. Tải lại hồ sơ.',
  'RepairEstimate.NegativeAmount': 'Chi phí không được âm.',
  'RepairEstimate.NoLines': 'Thêm ít nhất một hạng mục trước khi gửi duyệt.',
  'RepairEstimate.NotApproved': 'Phiên bản dự toán chưa được duyệt.',
  'RepairEstimate.NotDraft': 'Phiên bản này đã gửi duyệt nên không sửa được nữa. Lập phiên bản mới nếu cần thay đổi.',
  'RepairEstimate.NotFound': 'Không tìm thấy phiên bản dự toán.',
  'RepairEstimate.NotInspected': 'Chỉ lập dự toán sau khi giám định kết luận cần sửa.',
  'RepairEstimate.NotSubmitted': 'Chỉ ghi nhận quyết định cho phiên bản đang chờ duyệt.',
  'RepairEstimate.OpenVersion': 'Hồ sơ đang có phiên bản dự toán chưa xong (nháp hoặc chờ duyệt).',
  'RepairEstimate.PayerNameRequired': 'Ghi tên bên chịu phí khi chọn "Bên khác".',
  'RepairEstimate.PayerUndetermined': 'Chọn bên chịu phí trước khi đánh dấu đã thống nhất.',
  'RepairEstimate.ReasonRequired': 'Ghi lý do từ chối hoặc nội dung cần chỉnh sửa.',
  'RepairEstimate.TooManyLines': 'Tối đa 50 hạng mục mỗi dự toán.',
  'RepairEstimate.UnknownFinding': 'Hạng mục gắn với hư hỏng không thuộc hồ sơ này.',
  'RepairPhoto.FileMissing': 'File ảnh không còn trên máy chủ.',
  'RepairPhoto.FindingNotInCase': 'Hư hỏng được chọn không thuộc hồ sơ này.',
  'RepairPhoto.InvalidFile': 'File không phải ảnh JPG/PNG hợp lệ hoặc lớn hơn 5 MB.',
  'RepairPhoto.NotFound': 'Không tìm thấy ảnh.',
  'Partner.NoRole': 'Đối tác phải có ít nhất một vai trò.',
  'Partner.NotOperator': 'Đối tác này không có vai trò hãng khai thác.',
  'Partner.NotOwner': 'Đối tác này không có vai trò chủ sở hữu.',
  'Partner.OperatorRoleInUse': 'Đối tác còn quan hệ khai thác hoặc container trong bãi, chưa bỏ vai trò hãng khai thác được.',
  'Partner.OwnerRoleInUse': 'Đối tác còn sở hữu container, đổi chủ sở hữu các container đó trước.',
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
