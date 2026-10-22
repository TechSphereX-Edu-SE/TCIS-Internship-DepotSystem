/*
  Same rules as Domain/Services/ContainerNumberRules.cs, so the form can warn before calling the API:
  3-letter owner code + 'U' + 6-digit serial (not 000000) + check character.
  Check character: ISO 6346 Modulo 11, but remainder 10 -> 'X' (spec Appendix I).
*/
export type ContainerNumberCheck =
  | { valid: true; number: string; checkDigit: string }
  | { valid: false; number: string; reason: 'format' }
  | { valid: false; number: string; reason: 'checkDigit'; expected: string };

const FORMAT = /^[A-Z]{3}U\d{6}[\dX]$/;

export function normalizeContainerNumber(value: string | null | undefined): string {
  return (value ?? '').trim().toUpperCase();
}

export function checkContainerNumber(value: string | null | undefined): ContainerNumberCheck {
  const number = normalizeContainerNumber(value);
  if (!FORMAT.test(number) || number.slice(4, 10) === '000000') {
    return { valid: false, number, reason: 'format' };
  }

  const expected = calculateCheckDigit(number.slice(0, 10));
  return expected === number[10]
    ? { valid: true, number, checkDigit: expected }
    : { valid: false, number, reason: 'checkDigit', expected };
}

// Letter values A=10, B=12, C=13... (multiples of 11 skipped); character i is weighted by 2^i
export function calculateCheckDigit(firstTenCharacters: string): string {
  let sum = 0;
  for (let i = 0; i < 10; i++) {
    const c = firstTenCharacters[i];
    const value = /[A-Z]/.test(c) ? letterValue(c) : Number(c);
    sum += value * 2 ** i;
  }
  const remainder = sum % 11;
  return remainder === 10 ? 'X' : String(remainder);
}

function letterValue(letter: string): number {
  let value = 10;
  for (let code = 'A'.charCodeAt(0); code < letter.charCodeAt(0); code++) {
    value++;
    if (value % 11 === 0) value++;
  }
  return value;
}
