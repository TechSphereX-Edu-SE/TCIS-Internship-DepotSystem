import { ContainerCategory, ContainerSize } from './depot-models';

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

// Vietnamese message for an invalid number, shared by every screen that looks a container up by number
export function containerNumberError(check: ContainerNumberCheck): string | null {
  if (check.valid) return null;
  return check.reason === 'format'
    ? 'Số container gồm 3 chữ cái + U + 6 số + 1 ký tự kiểm tra (VD: MSCU1234566).'
    : `Sai check digit: với ${check.number.slice(0, 10)} ký tự cuối phải là ${check.expected}.`;
}

/*
  ISO 6346 size-type code, same rules as Domain/Services/IsoCodeRules.cs (4 characters):
    1. length: 2 = 20ft, 4 = 40ft
    2. height: 0 2 4 5 6 8 9, or C D E F (pallet-wide)
    3. type group letter, which must match the container type (Appendix II -> ISO group)
    4. type detail: a digit ISO defines for that group, or its alternate letter (A B D G J M V W X Y = 0..9)
  E.g. 22G1 = 20ft dry, 45R1 = 40ft high cube reefer.
*/
const ISO_LENGTH_CODE: Record<ContainerSize, string> = { Ft20: '2', Ft40: '4' };
const ISO_HEIGHT_CODES = '0245689CDEF';
const ISO_ALTERNATE_DETAILS = 'ABDGJMVWXY';

// Detail digits ISO 6346 defines per group (e.g. V only has V0, V2, V4)
const ISO_DEFINED_DETAILS: Record<string, string> = {
  G: '0123',
  V: '024',
  R: '0123',
  H: '01256',
  U: '012345',
  P: '012345',
  T: '0123456789',
  B: '013456',
  S: '012',
};

// Project mapping of Appendix II types to ISO groups (Bunker = tank T; Specialized also takes H and B)
export const ISO_TYPE_GROUPS: Record<ContainerCategory, string> = {
  Dry: 'G',
  Reefer: 'R',
  OpenTop: 'U',
  FlatRack: 'P',
  Bunker: 'T',
  Ventilated: 'V',
  Specialized: 'SHB',
};

// Vietnamese reason why the code does not fit the size and type, or null when it is valid
export function isoCodeError(value: string | null | undefined, size: ContainerSize, category: ContainerCategory): string | null {
  const code = (value ?? '').trim().toUpperCase();
  if (!/^[A-Z0-9]{2}[A-Z][A-Z0-9]$/.test(code)) {
    return 'ISO code gồm 4 ký tự: chiều dài, chiều cao, chữ nhóm loại, chi tiết loại (VD: 22G1, 45R1).';
  }

  const lengthCode = ISO_LENGTH_CODE[size];
  if (code[0] !== lengthCode) return `Container ${size === 'Ft20' ? '20ft' : '40ft'} có ISO code bắt đầu bằng ${lengthCode}.`;

  if (!ISO_HEIGHT_CODES.includes(code[1])) return 'Ký tự thứ 2 (chiều cao) phải là 0, 2, 4, 5, 6, 8, 9, C, D, E hoặc F.';

  const groups = ISO_TYPE_GROUPS[category];
  const group = code[2];
  if (!groups.includes(group)) return `Loại này dùng nhóm ${groups.split('').join(' hoặc ')} ở ký tự thứ 3 (đang là ${group}).`;

  if (!isDefinedDetail(group, code[3])) {
    const allowed = ISO_DEFINED_DETAILS[group].split('').map((d) => group + d).join(', ');
    return `${code.slice(2)} không có trong ISO 6346. Nhóm ${group} có: ${allowed}.`;
  }
  return null;
}

// Usual code for a size and type (standard height 8ft6, first group, detail 1 when defined): 22G1, 42R1, 22T1...
export function suggestIsoCode(size: ContainerSize, category: ContainerCategory): string {
  const group = ISO_TYPE_GROUPS[category][0];
  const detail = ISO_DEFINED_DETAILS[group].includes('1') ? '1' : '0';
  return `${ISO_LENGTH_CODE[size]}2${group}${detail}`;
}

// A detail is a defined digit, or the alternate letter of a defined digit
function isDefinedDetail(group: string, detail: string): boolean {
  if (/\d/.test(detail)) return ISO_DEFINED_DETAILS[group].includes(detail);
  const index = ISO_ALTERNATE_DETAILS.indexOf(detail);
  return index >= 0 && ISO_DEFINED_DETAILS[group].includes(String(index));
}
