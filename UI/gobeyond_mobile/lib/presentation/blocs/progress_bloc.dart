import 'package:flutter_bloc/flutter_bloc.dart';

class ProgressState {
  const ProgressState();
}

class ProgressBloc extends Cubit<ProgressState> {
  ProgressBloc() : super(const ProgressState());
}
