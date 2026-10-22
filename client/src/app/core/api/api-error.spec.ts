import { HttpErrorResponse } from '@angular/common/http';
import { fieldError, toApiError } from './api-error';

describe('toApiError', () => {
  it('maps a known error code to the Vietnamese message', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { title: 'Conflict', detail: 'Slot is taken.', errorCode: 'ContainerVisit.SlotOccupied' },
    });

    const result = toApiError(error);

    expect(result.status).toBe(409);
    expect(result.code).toBe('ContainerVisit.SlotOccupied');
    expect(result.message).toBe('Vị trí đã có container.');
    expect(result.detail).toBe('Slot is taken.');
  });

  it('keeps validation errors per field', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'One or more validation errors occurred.', errors: { ContainerNumber: ['Invalid check digit.'] } },
    });

    const result = toApiError(error);

    expect(result.message).toBe('Dữ liệu chưa hợp lệ, kiểm tra các ô được đánh dấu.');
    expect(fieldError(result, 'containerNumber')).toBe('Invalid check digit.');
  });

  it('explains when the API cannot be reached', () => {
    const result = toApiError(new HttpErrorResponse({ status: 0 }));

    expect(result.message).toContain('Không kết nối được API');
  });
});
