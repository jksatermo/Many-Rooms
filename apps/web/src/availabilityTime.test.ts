import { describe, expect, it } from 'vitest';
import { localDateTimeToUtc, toLocalDateTime } from './availabilityTime';

describe('availability local-time input', () => {
  it('converts the explicitly named device zone to an ISO instant and back', () => {
    expect(localDateTimeToUtc('2030-06-21T10:15')).toBe(
      '2030-06-21T15:15:00.000Z',
    );
    expect(toLocalDateTime('2030-06-21T15:15:00Z')).toBe('2030-06-21T10:15');
  });
  it('rejects dates that Date would silently normalize', () => {
    expect(() => localDateTimeToUtc('2030-02-30T10:00')).toThrow(
      'does not exist',
    );
    expect(() => localDateTimeToUtc('')).toThrow('valid start and end');
  });
  it('rejects the nonexistent spring-forward hour', () => {
    expect(() => localDateTimeToUtc('2030-03-10T02:30')).toThrow(
      'does not exist',
    );
  });
  it('uses the documented earlier occurrence during the repeated fall-back hour', () => {
    expect(localDateTimeToUtc('2030-11-03T01:30')).toBe(
      '2030-11-03T06:30:00.000Z',
    );
  });
});
