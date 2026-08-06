import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

@Component({ standalone: true, imports: [RouterLink, MatButtonModule, MatIconModule], templateUrl: './unauthorized.component.html', styleUrl: './unauthorized.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class UnauthorizedComponent {}
