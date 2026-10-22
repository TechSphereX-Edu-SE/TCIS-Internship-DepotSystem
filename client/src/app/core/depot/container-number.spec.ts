import { calculateCheckDigit, checkContainerNumber, containerNumberError } from './container-number';

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
