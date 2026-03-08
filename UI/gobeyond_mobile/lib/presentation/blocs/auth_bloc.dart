import 'package:flutter_bloc/flutter_bloc.dart';

class AuthState {
  const AuthState({this.isAuthenticated = false});

  final bool isAuthenticated;
}

class AuthBloc extends Cubit<AuthState> {
  AuthBloc() : super(const AuthState());

  void loggedIn() => emit(const AuthState(isAuthenticated: true));
  void loggedOut() => emit(const AuthState(isAuthenticated: false));
}
