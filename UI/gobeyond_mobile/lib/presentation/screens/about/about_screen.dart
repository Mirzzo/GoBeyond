import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../widgets/app_panel.dart';
import '../../widgets/gb_scaffold.dart';
import '../../widgets/gobeyond_logo.dart';

const _purposeText =
    'Svrha aplikacije GoBeyond je da poveže mentore (iskusne fitness trenere) s korisnicima koji '
    'žele unaprijediti svoju fizičku spremu, zdravlje i mentalnu otpornost. Aplikacija omogućava '
    'mentorima da dijele svoje znanje i iskustvo kroz personalizirane planove treninga, dok '
    'korisnicima pruža strukturiranu podršku na njihovom putu ka fizičkom i mentalnom napretku. '
    'Kroz jednostavan i intuitivan interfejs, GoBeyond olakšava kreiranje individualiziranih '
    'programa, komunikaciju između mentora i korisnika, praćenje napretka te postavljanje ciljeva. '
    'Osnovna ideja aplikacije jeste motivisati korisnike da svakim danom idu korak dalje, '
    'prevazilazeći lične granice i gradeći mentalnu i fizičku izdržljivost.\n\n'
    'GoBeyond nije samo fitness aplikacija – ona promoviše način života zasnovan na disciplini, '
    'dosljednosti i samoprevazilaženju.';

/// O nama — the "Svrha" section from the approved prijava-teme.txt.
class AboutScreen extends StatelessWidget {
  const AboutScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return GbScaffold(
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          const Center(child: GoBeyondLogo(fontSize: 36)),
          const SizedBox(height: 20),
          AppPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: const [
                Text('O NAMA',
                    style: TextStyle(
                        color: AppTheme.accent,
                        fontWeight: FontWeight.w800,
                        fontSize: 18)),
                SizedBox(height: 14),
                Text(_purposeText, style: TextStyle(height: 1.6)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
