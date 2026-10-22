import { calculateCheckDigit, checkContainerNumber, containerNumberError, isoCodeError, suggestIsoCode } from './container-number';

describe('container number rules', () => {
  it('accepts numbers with a correct check digit', () => {
    expect(checkContainerNumber('MSCU1234566')).toEqual({ valid: true, number: 'MSCU1234566', checkDigit: '6' });
    expect(checkContainerNumber(' mscu5551234 ').valid).toBe(true);
  });

  it('uses X when the remainder is 10 (spec Appendix I)', () => {
    expect(calculateCheckDigit('TCNU100845')).toBe('X');
    expect(checkContainerNumber('TCNU100845X').valid).toBe(true);
  });

  it('reports the expected check digit when it is wrong', () => {
    expect(checkContainerNumber('MSCU5551235')).toEqual({
      valid: false,
      number: 'MSCU5551235',
      reason: 'checkDigit',
      expected: '4',
    });
  });

  it('rejects a wrong format or serial 000000', () => {
    expect(checkContainerNumber('MSCJ1234566')).toMatchObject({ valid: false, reason: 'format' });
    expect(checkContainerNumber('MSCU0000000')).toMatchObject({ valid: false, reason: 'format' });
    expect(checkContainerNumber('MSCU12')).toMatchObject({ valid: false, reason: 'format' });
  });
});

it('explains an invalid number in Vietnamese, nothing for a valid one', () => {
  expect(containerNumberError(checkContainerNumber('MSCU1234566'))).toBeNull();
  expect(containerNumberError(checkContainerNumber('MSCU12'))).toContain('3 chữ cái + U + 6 số');
  expect(containerNumberError(checkContainerNumber('MSCU5551235'))).toBe(
    'Sai check digit: với MSCU555123 ký tự cuối phải là 4.',
  );
});

describe('ISO size-type code (same as IsoCodeRules)', () => {
  it('accepts codes that fit the size and the type', () => {
    expect(isoCodeError('22G1', 'Ft20', 'Dry')).toBeNull();
    expect(isoCodeError(' 45r1 ', 'Ft40', 'Reefer')).toBeNull();
    expect(isoCodeError('22T6', 'Ft20', 'Bunker')).toBeNull();
    expect(isoCodeError('22H0', 'Ft20', 'Specialized')).toBeNull();
    // Alternate letter B = detail 1
    expect(isoCodeError('22GB', 'Ft20', 'Dry')).toBeNull();
  });

  it('explains each wrong character', () => {
    expect(isoCodeError('22G', 'Ft20', 'Dry')).toContain('4 ký tự');
    expect(isoCodeError('42G1', 'Ft20', 'Dry')).toBe('Container 20ft có ISO code bắt đầu bằng 2.');
    expect(isoCodeError('23G1', 'Ft20', 'Dry')).toContain('chiều cao');
    expect(isoCodeError('22R1', 'Ft20', 'Dry')).toBe('Loại này dùng nhóm G ở ký tự thứ 3 (đang là R).');
    expect(isoCodeError('22V1', 'Ft20', 'Ventilated')).toBe('V1 không có trong ISO 6346. Nhóm V có: V0, V2, V4.');
  });

  it('suggests the usual code for a size and type', () => {
    expect(suggestIsoCode('Ft20', 'Dry')).toBe('22G1');
    expect(suggestIsoCode('Ft40', 'Reefer')).toBe('42R1');
    expect(suggestIsoCode('Ft20', 'Ventilated')).toBe('22V0');
    expect(suggestIsoCode('Ft20', 'Specialized')).toBe('22S1');
  });
});
