import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { RepairCaseDetail, RepairEstimate } from '../../core/depot/depot-models';
import { RepairApi } from '../../core/depot/repair-api';
import { RepairEstimatePanel } from './repair-estimate-panel';

// Case waiting for its estimate, with one reported damage
function caseWith(estimates: RepairEstimate[], canCreateEstimate = false): RepairCaseDetail {
  return {
    id: 'case-1',
    containerId: 'c-1',
    containerNumber: 'MSCU1234566',
    isoCode: '22G1',
    size: 'Ft20',
    category: 'Dry',
    ownerCode: 'MSC',
    status: 'AwaitingEstimateApproval',
    repairNeeded: true,
    openedAt: '2026-10-22T03:00:00Z',
    closedAt: null,
    visitId: 'v-1',
    operatorCode: 'HMM',
    conditionAtGateIn: 'Damaged',
    inYard: true,
    blockCode: 'A',
    bay: 1,
    row: 1,
    tier: 1,
    allowedActions: [],
    findings: [{ id: 'f-1', description: 'Móp cửa', source: 'GateIn', visitId: 'v-1', reportedAt: '2026-10-22T03:00:00Z', reportedBy: null }],
    photos: [],
    history: [],
    canRequestExternal: false,
    transfers: [],
    estimates,
    canCreateEstimate,
  };
}

function estimate(status: RepairEstimate['status'], lines: RepairEstimate['lines'] = []): RepairEstimate {
  return {
    id: 'e-1',
    version: 1,
    status,
    currency: 'VND',
    total: lines.reduce((s, l) => s + l.amount, 0),
    note: null,
    evidence: null,
    createdAt: '2026-10-22T03:00:00Z',
    createdBy: 'staff@depot.test',
    submittedAt: null,
    submittedBy: null,
    decidedAt: null,
    decisionRecordedBy: null,
    approverParty: null,
    approverName: null,
    approvalReference: null,
    decisionNote: null,
    lines,
    payerTotals: [],
  };
}

describe('RepairEstimatePanel (feedback 4)', () => {
  const api = {
    createEstimate: vi.fn(() => of({ id: 'e-2' })),
    updateEstimate: vi.fn(() => of(undefined)),
    submitEstimate: vi.fn(() => of(undefined)),
    decideEstimate: vi.fn(() => of(undefined)),
    setLinePayer: vi.fn(() => of(undefined)),
  };

  function render(detail: RepairCaseDetail) {
    TestBed.configureTestingModule({ providers: [{ provide: RepairApi, useValue: api }] });
    const fixture = TestBed.createComponent(RepairEstimatePanel);
    fixture.componentRef.setInput('detail', detail);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => vi.clearAllMocks());

  it('saves the draft table and then submits it', () => {
    const fixture = render(caseWith([estimate('Draft')]));
    const element: HTMLElement = fixture.nativeElement;

    // A new first version starts with one empty item; fill it in
    const set = (id: string, value: string) => {
      const input = element.querySelector<HTMLInputElement>(`#${id}`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    set('lineDamage0', 'Móp cửa');
    set('lineMethod0', 'Nắn, sơn lại');
    set('lineAmount0', '1500000');
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.draft .btn-primary')!.click();

    expect(api.updateEstimate).toHaveBeenCalledWith('e-1', {
      currency: 'VND',
      note: null,
      evidence: null,
      lines: [
        { findingId: null, damageDescription: 'Móp cửa', repairMethod: 'Nắn, sơn lại', amount: 1500000, payer: 'LineOperator', payerName: null },
      ],
    });
    expect(api.submitEstimate).toHaveBeenCalledWith('e-1');
  });

  it('does not send an incomplete item, and "Bên khác" needs a name', () => {
    const fixture = render(caseWith([estimate('Draft')]));
    const element: HTMLElement = fixture.nativeElement;
    const payer = element.querySelector<HTMLSelectElement>('#linePayer0')!;
    payer.value = 'Other';
    payer.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    element.querySelector<HTMLButtonElement>('.draft .btn-primary')!.click();
    fixture.detectChanges();

    expect(api.updateEstimate).not.toHaveBeenCalled();
    expect(element.querySelector('.draft .field-error')?.textContent).toContain('Bên khác');
  });

  it('asks for the approving party before recording an approval', () => {
    const submitted = estimate('Submitted', [
      { id: 'l-1', position: 1, findingId: 'f-1', damageDescription: 'Móp cửa', repairMethod: 'Sơn', amount: 1000000, payer: 'LineOperator', payerName: null, payerStatus: 'Expected', payerNote: null },
    ]);
    const fixture = render(caseWith([submitted]));
    const element: HTMLElement = fixture.nativeElement;

    // The line operator of the visit is offered as the approving party
    const party = element.querySelector<HTMLInputElement>('#approverParty')!;
    expect(party.value).toBe('HMM');
    party.value = '';
    party.dispatchEvent(new Event('input'));
    element.querySelector<HTMLButtonElement>('.decision-form .btn-primary')!.click();
    fixture.detectChanges();
    expect(api.decideEstimate).not.toHaveBeenCalled();

    party.value = 'HMM';
    party.dispatchEvent(new Event('input'));
    element.querySelector<HTMLButtonElement>('.decision-form .btn-primary')!.click();
    expect(api.decideEstimate).toHaveBeenCalledWith('e-1', {
      decision: 'Approve',
      approverParty: 'HMM',
      approverName: null,
      approvalReference: null,
      note: null,
    });
  });

  it('offers a new version copied from the latest one', () => {
    const fixture = render(caseWith([estimate('Rejected')], true));
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.create .btn-primary')!;

    expect(button.textContent).toContain('từ v1');
    button.click();
    expect(api.createEstimate).toHaveBeenCalledWith('case-1', 'VND', null, null);
  });
});
