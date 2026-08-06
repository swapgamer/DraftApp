import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { map } from 'rxjs';
import { ConfirmationDialogComponent } from '../shared/confirmation-dialog/confirmation-dialog.component';

@Injectable({ providedIn: 'root' })
export class ConfirmationService {
  private readonly dialog = inject(MatDialog);
  ask(title: string, message: string, confirmLabel: string) {
    return this.dialog.open(ConfirmationDialogComponent, { data: { title, message, confirmLabel }, width: '24rem', autoFocus: false }).afterClosed().pipe(map(Boolean));
  }
}
