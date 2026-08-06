import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ChemistryCombination, ChemistryService, ChemistryType } from '../../core/chemistry.service';
import { ChemistryCombinationComponent } from './chemistry-combination.component';

@Component({standalone:true,imports:[FormsModule,MatButtonModule,MatFormFieldModule,MatIconModule,MatInputModule,MatSelectModule,ChemistryCombinationComponent],templateUrl:'./chemistry.component.html',styleUrl:'./chemistry.component.scss',changeDetection:ChangeDetectionStrategy.OnPush})
export class ChemistryComponent {
  private readonly chemistry=inject(ChemistryService);
  readonly type=signal<ChemistryType|null>(null); readonly search=signal(''); readonly browseResults=signal<ChemistryCombination[]>([]); readonly duos=signal<ChemistryCombination[]>([]); readonly trios=signal<ChemistryCombination[]>([]); readonly loading=signal(false); readonly searched=signal(false); readonly error=signal('');
  browse():void { const type=this.type(); if(!type) return; this.loading.set(true);this.searched.set(false);this.error.set('');this.chemistry.browse(type).subscribe({next:result=>this.browseResults.set(result.items),error:()=>this.error.set('We could not load chemistry combinations.'),complete:()=>this.loading.set(false)}); }
  searchPlayer():void { const term=this.search().trim();if(!term)return;this.loading.set(true);this.error.set('');this.searched.set(true);this.chemistry.search(term).subscribe({next:result=>{this.duos.set(result.duos);this.trios.set(result.trios);},error:()=>this.error.set('We could not search chemistry combinations.'),complete:()=>this.loading.set(false)}); }
}
