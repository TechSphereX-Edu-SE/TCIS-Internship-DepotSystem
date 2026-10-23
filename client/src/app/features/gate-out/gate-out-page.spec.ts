import { DeliveryOrder } from '../../core/depot/depot-models';
import { lineFor, remainingSummary } from './gate-out-page';

describe('gate-out order helpers', () => {
  const order = {
    lines: [
      { category: 'Dry', size: 'Ft20', quantity: 3, releasedQuantity: 1, remainingQuantity: 2 },
      { category: 'Dry', size: 'Ft40', quantity: 1, releasedQuantity: 1, remainingQuantity: 0 },
      { category: 'Reefer', size: null, quantity: 2, releasedQuantity: 0, remainingQuantity: 2 },
    ],
  } as Pick<DeliveryOrder, 'lines'>;

  it('finds the line of the same type and size (Q1)', () => {
    expect(lineFor(order, { category: 'Dry', size: 'Ft40' })?.remainingQuantity).toBe(0);
    expect(lineFor(order, { category: 'Dry', size: 'Ft20' })?.remainingQuantity).toBe(2);
    expect(lineFor(order, { category: 'OpenTop', size: 'Ft20' })).toBeNull();
  });

  it('lets a line without size take both sizes', () => {
    expect(lineFor(order, { category: 'Reefer', size: 'Ft40' })?.quantity).toBe(2);
  });

  it('lists only what the order still needs', () => {
    expect(remainingSummary(order)).toBe('Dry 20ft còn 2/3 · Reefer 20/40ft còn 2/2');
  });
});
