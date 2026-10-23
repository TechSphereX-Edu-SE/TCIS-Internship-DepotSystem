import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { map, startWith } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import { Block, CATEGORIES, CATEGORY_HINT, CATEGORY_LABEL, ContainerCategory, categoriesText } from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { Pager } from '../../shared/pager/pager';
import { ToastService } from '../../shared/toast/toast-service';

const PAGE_SIZE = 20;

// Same as Block.MinTiers: tiers stack "from 2 to many" (spec section 1)
const MIN_TIERS = 2;

// Whole number at least `min`, for the bay / row / tier fields
const sizeValidators = (min: number) => [Validators.required, Validators.min(min), Validators.pattern(/^\d+$/)];

/*
  Spec section 1: yard blocks. A physical block has bays x rows x tiers (bay = 20ft unit,
  odd bays hold 20ft, even bays 40ft); a virtual block (e.g. WAIT for repair) has no positions.
  The API has no update for code or description, only a resize of a physical block, so the form
  has two modes: create a block, or resize the block picked in the list.
  Each block lists the container types it takes (feedback 3); gate-in and move check that list.
*/
@Component({
  selector: 'app-blocks-page',
  imports: [ReactiveFormsModule, Pager],
  templateUrl: './blocks-page.html',
})
export class BlocksPage {
  private readonly api = inject(MasterDataApi);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly minTiers = MIN_TIERS;

  // Same rules as CreateBlockValidator; the sizes are disabled (and sent as null) for a virtual block
  protected readonly createForm = this.fb.group({
    code: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9]{1,10}$/)]],
    description: ['', Validators.maxLength(200)],
    isVirtual: [false],
    maxBays: new FormControl<number | null>(null, sizeValidators(1)),
    maxRows: new FormControl<number | null>(null, sizeValidators(1)),
    maxTiers: new FormControl<number | null>(null, sizeValidators(MIN_TIERS)),
    // Feedback 3: the types this block takes; at least one (same rule as CreateBlockValidator)
    allowedCategories: new FormControl<ContainerCategory[]>([], {
      nonNullable: true,
      validators: (control) => (control.value.length === 0 ? { required: true } : null),
    }),
  });

  protected readonly categories = CATEGORIES;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly categoryHint = CATEGORY_HINT;
  protected readonly categoriesText = categoriesText;

  // Same rules as ResizeBlockValidator
  protected readonly resizeForm = this.fb.group({
    maxBays: [1, sizeValidators(1)],
    maxRows: [1, sizeValidators(1)],
    maxTiers: [MIN_TIERS, sizeValidators(MIN_TIERS)],
  });

  protected readonly items = signal<Block[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);
  // Physical block being resized; null = the form creates a new block
  protected readonly resizing = signal<Block | null>(null);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  protected readonly isVirtual = toSignal(
    this.createForm.controls.isVirtual.valueChanges.pipe(startWith(false)),
    { initialValue: false },
  );

  // "20 bay × 6 row × 5 tier = 300 chỗ 20ft": helps check a size before saving
  private readonly resizeValue = toSignal(
    this.resizeForm.valueChanges.pipe(
      startWith(null),
      map(() => this.resizeForm.getRawValue()),
    ),
    { initialValue: this.resizeForm.getRawValue() },
  );

  // Shrinking can fail (409) when a container stands outside the new size; say so before trying
  protected readonly shrinking = computed(() => {
    const block = this.resizing();
    const v = this.resizeValue();
    if (!block || block.containersInYard === 0) return false;
    return v.maxBays < block.maxBays! || v.maxRows < block.maxRows! || v.maxTiers < block.maxTiers!;
  });

  constructor() {
    this.load(1);
    // Virtual block: no sizes at all (CreateBlockValidator requires them to be null)
    this.createForm.controls.isVirtual.valueChanges.pipe(takeUntilDestroyed()).subscribe((virtual) => {
      const sizes = [this.createForm.controls.maxBays, this.createForm.controls.maxRows, this.createForm.controls.maxTiers];
      sizes.forEach((control) => {
        if (virtual) {
          control.setValue(null);
          control.disable();
        } else {
          control.enable();
        }
      });
    });
  }

  protected load(page: number): void {
    this.api.blockPage(page, PAGE_SIZE).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.page.set(page);
        this.listError.set(null);
      },
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
  }

  protected sizeText(block: Block): string {
    return block.isVirtual ? 'Không quản lý bay/row/tier' : `${block.maxBays} bay × ${block.maxRows} row × ${block.maxTiers} tier`;
  }

  // A virtual block has nothing to resize, so only physical blocks get the button
  protected startResize(block: Block): void {
    this.resizing.set(block);
    this.resizeForm.reset({ maxBays: block.maxBays!, maxRows: block.maxRows!, maxTiers: block.maxTiers! });
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // Back to "create" mode
  protected cancel(): void {
    this.resizing.set(null);
    this.createForm.reset({ isVirtual: false, allowedCategories: [] });
    this.submitted.set(false);
    this.apiError.set(null);
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);
    const block = this.resizing();
    if (block) this.saveResize(block);
    else this.saveCreate();
  }

  // Ticks or unticks one type; the list is kept in enum order so the request is stable
  protected toggleCategory(category: ContainerCategory, checked: boolean): void {
    const control = this.createForm.controls.allowedCategories;
    const next = checked ? [...control.value, category] : control.value.filter((c) => c !== category);
    control.setValue(CATEGORIES.filter((c) => next.includes(c)));
    control.markAsTouched();
  }

  protected invalid(
    form: 'create' | 'resize',
    name: 'code' | 'description' | 'maxBays' | 'maxRows' | 'maxTiers' | 'allowedCategories',
  ): boolean {
    // Typed as AbstractControl: the two forms have different value types
    const control: AbstractControl | null = form === 'create' ? this.createForm.get(name) : this.resizeForm.get(name);
    return !!control && control.enabled && control.invalid && (control.touched || this.submitted());
  }

  private saveCreate(): void {
    this.createForm.markAllAsTouched();
    if (this.createForm.invalid) return;

    const v = this.createForm.getRawValue();
    const code = v.code.trim().toUpperCase();
    this.submitting.set(true);
    this.api
      .createBlock({
        code,
        description: v.description.trim() || null,
        isVirtual: v.isVirtual,
        maxBays: v.isVirtual ? null : Number(v.maxBays),
        maxRows: v.isVirtual ? null : Number(v.maxRows),
        maxTiers: v.isVirtual ? null : Number(v.maxTiers),
        allowedCategories: v.allowedCategories,
      })
      .subscribe({
        next: () => this.done(`Đã thêm block ${code}`),
        error: (err: unknown) => this.failed(err),
      });
  }

  private saveResize(block: Block): void {
    this.resizeForm.markAllAsTouched();
    if (this.resizeForm.invalid) return;

    const v = this.resizeForm.getRawValue();
    this.submitting.set(true);
    this.api
      .resizeBlock(block.id, { maxBays: Number(v.maxBays), maxRows: Number(v.maxRows), maxTiers: Number(v.maxTiers) })
      .subscribe({
        next: () => this.done(`Đã đổi kích thước block ${block.code}`),
        error: (err: unknown) => this.failed(err),
      });
  }

  private done(message: string): void {
    this.toast.success(message);
    this.cancel();
    this.load(this.page());
    this.submitting.set(false);
  }

  private failed(err: unknown): void {
    this.apiError.set(toApiError(err));
    this.submitting.set(false);
  }
}
